param(
    [switch]$Preview,
    [switch]$Run,
    [switch]$NoPause,
    [switch]$ValidateGui
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$script:AppRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$script:ConfigPath = Join-Path $script:AppRoot 'close-user-apps.config.json'

function ConvertTo-NameSet {
    param([object[]]$Names)

    $set = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($name in @($Names)) {
        if (-not [string]::IsNullOrWhiteSpace([string]$name)) {
            [void]$set.Add(([string]$name).Trim())
        }
    }
    return ,$set
}

function Read-AppConfig {
    if (-not (Test-Path -LiteralPath $script:ConfigPath)) {
        throw "找不到配置文件：$script:ConfigPath"
    }

    $raw = Get-Content -LiteralPath $script:ConfigPath -Raw -Encoding UTF8
    $config = $raw | ConvertFrom-Json

    [pscustomobject]@{
        WaitSeconds       = [int]$config.waitSeconds
        TargetNames       = ConvertTo-NameSet $config.targetNames
        ProtectedNames    = ConvertTo-NameSet $config.protectedNames
        ForceAllowedNames = ConvertTo-NameSet $config.forceAllowedNames
    }
}

function Get-ParentProcessId {
    param([int]$ProcessId)

    try {
        $proc = Get-CimInstance Win32_Process -Filter "ProcessId=$ProcessId" -ErrorAction Stop
        if ($proc) {
            return [int]$proc.ParentProcessId
        }
    }
    catch {
        return $null
    }
    return $null
}

function Test-SystemPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }

    $windows = [Environment]::GetFolderPath('Windows')
    return $Path.StartsWith($windows, [System.StringComparison]::OrdinalIgnoreCase)
}

function Ensure-NativeWindowApi {
    if ('OneClickClose.NativeWindow' -as [type]) {
        return
    }

    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace OneClickClose
{
    public static class NativeWindow
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    }
}
'@
}

function Get-VisibleWindowProcessIdSet {
    Ensure-NativeWindowApi

    $ids = New-Object 'System.Collections.Generic.HashSet[int]'
    $callback = [OneClickClose.NativeWindow+EnumWindowsProc]{
        param([IntPtr]$hWnd, [IntPtr]$lParam)

        if ([OneClickClose.NativeWindow]::IsWindowVisible($hWnd)) {
            [uint32]$windowProcessId = 0
            [void][OneClickClose.NativeWindow]::GetWindowThreadProcessId($hWnd, [ref]$windowProcessId)
            if ($windowProcessId -gt 0) {
                [void]$ids.Add([int]$windowProcessId)
            }
        }
        return $true
    }

    [void][OneClickClose.NativeWindow]::EnumWindows($callback, [IntPtr]::Zero)
    return ,$ids
}

function Send-CloseToProcessWindows {
    param([int]$ProcessId)

    Ensure-NativeWindowApi

    $sent = 0
    $callback = [OneClickClose.NativeWindow+EnumWindowsProc]{
        param([IntPtr]$hWnd, [IntPtr]$lParam)

        [uint32]$windowProcessId = 0
        [void][OneClickClose.NativeWindow]::GetWindowThreadProcessId($hWnd, [ref]$windowProcessId)
        if (($windowProcessId -eq [uint32]$ProcessId) -and [OneClickClose.NativeWindow]::IsWindowVisible($hWnd)) {
            [void][OneClickClose.NativeWindow]::PostMessage($hWnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)
            $script:OneClickCloseSentWindowCount++
        }
        return $true
    }

    $script:OneClickCloseSentWindowCount = 0
    [void][OneClickClose.NativeWindow]::EnumWindows($callback, [IntPtr]::Zero)
    $sent = $script:OneClickCloseSentWindowCount
    Remove-Variable -Name OneClickCloseSentWindowCount -Scope Script -ErrorAction SilentlyContinue
    return $sent
}

function Test-CodexToolProcess {
    param(
        [string]$ProcessName,
        [string]$Path
    )

    if ($ProcessName -ine 'codex') {
        return $false
    }

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }

    return $Path.IndexOf('\AppData\Local\OpenAI\Codex\bin\', [System.StringComparison]::OrdinalIgnoreCase) -ge 0
}

function New-ProcessRecord {
    param(
        [System.Diagnostics.Process]$Process,
        [bool]$HasWindow,
        [string]$Action,
        [string]$Reason,
        [string]$Status
    )

    $path = $null
    try {
        $path = $Process.Path
    }
    catch {
        $path = ''
    }

    [pscustomobject]@{
        Id              = [int]$Process.Id
        ProcessName     = [string]$Process.ProcessName
        MainWindowTitle = [string]$Process.MainWindowTitle
        Path            = [string]$path
        HasWindow       = [bool]$HasWindow
        Action          = [string]$Action
        Reason          = [string]$Reason
        Status          = [string]$Status
    }
}

function Get-ClosePlan {
    $config = Read-AppConfig
    $parentId = Get-ParentProcessId -ProcessId $PID
    $excludeIds = New-Object 'System.Collections.Generic.HashSet[int]'
    [void]$excludeIds.Add([int]$PID)
    if ($parentId) {
        [void]$excludeIds.Add([int]$parentId)
    }

    $candidates = New-Object System.Collections.Generic.List[object]
    $protected = New-Object System.Collections.Generic.List[object]
    $skipped = New-Object System.Collections.Generic.List[object]
    $visibleWindowProcessIds = Get-VisibleWindowProcessIdSet

    foreach ($proc in @(Get-Process | Sort-Object ProcessName, Id)) {
        $name = [string]$proc.ProcessName
        $path = ''
        try {
            $path = [string]$proc.Path
        }
        catch {
            $path = ''
        }
        $hasWindow = ($proc.MainWindowHandle -ne [IntPtr]::Zero) -or $visibleWindowProcessIds.Contains([int]$proc.Id)

        if ($excludeIds.Contains([int]$proc.Id)) {
            $skipped.Add((New-ProcessRecord -Process $proc -HasWindow $hasWindow -Action '跳过' -Reason '当前工具进程' -Status 'skipped'))
            continue
        }

        if (Test-CodexToolProcess -ProcessName $name -Path $path) {
            $protected.Add((New-ProcessRecord -Process $proc -HasWindow $hasWindow -Action '保护' -Reason 'Codex 工具子进程' -Status 'protected'))
            continue
        }

        if ($config.ProtectedNames.Contains($name) -or (Test-SystemPath -Path $path)) {
            $protected.Add((New-ProcessRecord -Process $proc -HasWindow $hasWindow -Action '保护' -Reason '保护名单或系统路径' -Status 'protected'))
            continue
        }

        $isTarget = $config.TargetNames.Contains($name)
        $isForceAllowed = $config.ForceAllowedNames.Contains($name)
        if (-not ($isTarget -or $isForceAllowed)) {
            $skipped.Add((New-ProcessRecord -Process $proc -HasWindow $hasWindow -Action '跳过' -Reason '不在关闭名单' -Status 'skipped'))
            continue
        }

        if ($hasWindow) {
            $candidates.Add((New-ProcessRecord -Process $proc -HasWindow $hasWindow -Action '温和关闭' -Reason '有窗口，先发送关闭请求' -Status 'candidate'))
        }
        elseif ($isForceAllowed) {
            $candidates.Add((New-ProcessRecord -Process $proc -HasWindow $hasWindow -Action '强制清理' -Reason '后台辅助进程，允许强制结束' -Status 'candidate'))
        }
        elseif ($name -ieq 'powershell' -or $name -ieq 'pwsh' -or $name -ieq 'cmd') {
            $protected.Add((New-ProcessRecord -Process $proc -HasWindow $hasWindow -Action '保护' -Reason '无窗口终端，避免误关脚本/任务' -Status 'protected'))
        }
        else {
            $candidates.Add((New-ProcessRecord -Process $proc -HasWindow $hasWindow -Action '只提示' -Reason '无窗口，不在强制名单' -Status 'candidate'))
        }
    }

    return (New-Object PSObject -Property @{
        Config     = $config
        Candidates = @($candidates.ToArray())
        Protected  = @($protected.ToArray())
        Skipped    = @($skipped.ToArray())
    })
}

function Group-PlanRows {
    param([object[]]$Records)

    @($Records | Group-Object ProcessName, Action | Sort-Object Name | ForEach-Object {
        $first = $_.Group[0]
        [pscustomobject]@{
            Process = $first.ProcessName
            Count   = $_.Count
            Action  = $first.Action
            Note    = $first.Reason
        }
    })
}

function Get-ActionSummary {
    param([object[]]$Records)

    $graceful = @($Records | Where-Object { $_.Action -eq '温和关闭' }).Count
    $force = @($Records | Where-Object { $_.Action -eq '强制清理' }).Count
    $report = @($Records | Where-Object { $_.Action -eq '只提示' }).Count

    "温和关闭 $graceful 个，强制清理 $force 个，只提示 $report 个"
}

function Format-PlanText {
    param([object]$Plan)

    $rows = Group-PlanRows -Records $Plan.Candidates
    $protectedRows = Group-PlanRows -Records $Plan.Protected

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("一键关闭后台软件 - 预览")
    $lines.Add(("待处理进程：{0}；保护进程：{1}" -f @($Plan.Candidates).Count, @($Plan.Protected).Count))
    $lines.Add((Get-ActionSummary -Records $Plan.Candidates))
    $lines.Add('')
    $lines.Add('将处理：')
    foreach ($row in $rows) {
        $lines.Add(("  {0,-28} x{1,-3} {2} - {3}" -f $row.Process, $row.Count, $row.Action, $row.Note))
    }
    $lines.Add('')
    $lines.Add('已保护：')
    foreach ($row in @($protectedRows | Select-Object -First 60)) {
        $lines.Add(("  {0,-28} x{1,-3} {2}" -f $row.Process, $row.Count, $row.Note))
    }
    return ($lines -join [Environment]::NewLine)
}

function Test-ProcessAlive {
    param([int]$Id)

    try {
        return [bool](Get-Process -Id $Id -ErrorAction Stop)
    }
    catch {
        return $false
    }
}

function Invoke-ClosePlan {
    param(
        [object]$Plan,
        [scriptblock]$Logger
    )

    if (-not $Logger) {
        $Logger = { param([string]$Message) Write-Host $Message }
    }

    $attemptedGraceful = New-Object System.Collections.Generic.List[object]
    $forceTargets = New-Object System.Collections.Generic.List[object]
    $reportOnly = New-Object System.Collections.Generic.List[object]

    & $Logger ("开始处理：{0}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
    & $Logger ("跳过保护：{0} 个进程" -f @($Plan.Protected).Count)

    foreach ($item in @($Plan.Candidates)) {
        if ($item.Action -eq '温和关闭') {
            try {
                $proc = Get-Process -Id $item.Id -ErrorAction Stop
                $sentWindows = Send-CloseToProcessWindows -ProcessId $item.Id
                if ($sentWindows -gt 0 -or $proc.MainWindowHandle -ne [IntPtr]::Zero) {
                    if ($proc.MainWindowHandle -ne [IntPtr]::Zero) {
                        [void]$proc.CloseMainWindow()
                    }
                    $attemptedGraceful.Add($item)
                    & $Logger ("已发送关闭请求：{0} ({1})" -f $item.ProcessName, $item.Id)
                }
                else {
                    $reportOnly.Add($item)
                    & $Logger ("无窗口，只提示：{0} ({1})" -f $item.ProcessName, $item.Id)
                }
            }
            catch {
                & $Logger ("已退出或无法访问：{0} ({1})" -f $item.ProcessName, $item.Id)
            }
        }
        elseif ($item.Action -eq '强制清理') {
            $forceTargets.Add($item)
        }
        else {
            $reportOnly.Add($item)
            & $Logger ("只提示不强杀：{0} ({1})" -f $item.ProcessName, $item.Id)
        }
    }

    if (@($attemptedGraceful).Count -gt 0) {
        & $Logger ("等待程序自行退出：{0} 秒" -f $Plan.Config.WaitSeconds)
        Start-Sleep -Seconds $Plan.Config.WaitSeconds
    }

    foreach ($item in @($attemptedGraceful)) {
        if ((Test-ProcessAlive -Id $item.Id) -and $Plan.Config.ForceAllowedNames.Contains($item.ProcessName)) {
            $forceTargets.Add($item)
        }
    }

    $forced = New-Object System.Collections.Generic.List[object]
    foreach ($item in @($forceTargets)) {
        if (-not (Test-ProcessAlive -Id $item.Id)) {
            continue
        }

        try {
            Stop-Process -Id $item.Id -Force -ErrorAction Stop
            $forced.Add($item)
            & $Logger ("已强制关闭：{0} ({1})" -f $item.ProcessName, $item.Id)
        }
        catch {
            & $Logger ("强制关闭失败：{0} ({1}) - {2}" -f $item.ProcessName, $item.Id, $_.Exception.Message)
        }
    }

    $closedGraceful = @($attemptedGraceful | Where-Object { -not (Test-ProcessAlive -Id $_.Id) -and -not $forced.Contains($_) })
    $remaining = @($Plan.Candidates | Where-Object { Test-ProcessAlive -Id $_.Id })

    & $Logger ''
    & $Logger ("结果：已温和关闭 {0} 个；已强制关闭 {1} 个；跳过保护 {2} 个；仍在运行 {3} 个" -f @($closedGraceful).Count, @($forced).Count, @($Plan.Protected).Count, @($remaining).Count)
    if (@($remaining).Count -gt 0) {
        & $Logger '仍在运行：'
        foreach ($item in @($remaining | Sort-Object ProcessName, Id)) {
            & $Logger ("  {0} ({1}) - {2}" -f $item.ProcessName, $item.Id, $item.Reason)
        }
    }
}

function Show-CliPreview {
    $plan = Get-ClosePlan
    Write-Host (Format-PlanText -Plan $plan)
}

function Invoke-CliRun {
    $plan = Get-ClosePlan
    Write-Host (Format-PlanText -Plan $plan)
    Write-Host ''
    $answer = Read-Host '确认关闭以上程序？输入 Y 后回车'
    if ($answer -notin @('Y', 'y')) {
        Write-Host '已取消。'
        return
    }
    Invoke-ClosePlan -Plan $plan -Logger { param([string]$Message) Write-Host $Message }
}

function Add-UiLog {
    param(
        [System.Windows.Controls.TextBox]$TextBox,
        [string]$Message
    )

    $TextBox.AppendText($Message + [Environment]::NewLine)
    $TextBox.ScrollToEnd()
}

function Show-Gui {
    param([switch]$ValidateOnly)

    Add-Type -AssemblyName PresentationFramework
    Add-Type -AssemblyName PresentationCore
    Add-Type -AssemblyName WindowsBase

    [xml]$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="一键关闭后台软件"
        Width="980"
        Height="680"
        MinWidth="880"
        MinHeight="620"
        WindowStartupLocation="CenterScreen"
        Background="#F4F0EA"
        FontFamily="Microsoft YaHei UI">
  <Window.Resources>
    <Style TargetType="Button">
      <Setter Property="MinHeight" Value="38"/>
      <Setter Property="Padding" Value="16,8"/>
      <Setter Property="Margin" Value="6,0,0,0"/>
      <Setter Property="FontSize" Value="14"/>
      <Setter Property="Cursor" Value="Hand"/>
      <Setter Property="BorderThickness" Value="0"/>
      <Setter Property="Foreground" Value="#1C2524"/>
      <Setter Property="Background" Value="#D9E6DF"/>
    </Style>
    <Style x:Key="PrimaryButton" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
      <Setter Property="Background" Value="#0F766E"/>
      <Setter Property="Foreground" Value="White"/>
      <Setter Property="FontWeight" Value="SemiBold"/>
    </Style>
    <Style x:Key="DangerButton" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
      <Setter Property="Background" Value="#B9462D"/>
      <Setter Property="Foreground" Value="White"/>
      <Setter Property="FontWeight" Value="SemiBold"/>
    </Style>
    <Style x:Key="Card" TargetType="Border">
      <Setter Property="CornerRadius" Value="8"/>
      <Setter Property="Background" Value="#FFFFFF"/>
      <Setter Property="BorderBrush" Value="#DDD7CE"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="Padding" Value="16"/>
      <Setter Property="Margin" Value="0,0,12,12"/>
    </Style>
    <Style x:Key="MetricLabel" TargetType="TextBlock">
      <Setter Property="Foreground" Value="#6F655D"/>
      <Setter Property="FontSize" Value="12"/>
    </Style>
    <Style x:Key="MetricValue" TargetType="TextBlock">
      <Setter Property="Foreground" Value="#1C2524"/>
      <Setter Property="FontSize" Value="28"/>
      <Setter Property="FontWeight" Value="SemiBold"/>
    </Style>
  </Window.Resources>
  <Grid Margin="24">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>

    <Grid Grid.Row="0" Margin="0,0,0,18">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
      </Grid.ColumnDefinitions>
      <StackPanel>
        <TextBlock Text="一键关闭后台软件" FontSize="30" FontWeight="Bold" Foreground="#17201F"/>
        <TextBlock Text="先预览，再温和关闭；保留密码、同步、代理、远控和系统服务。" FontSize="14" Foreground="#6F655D" Margin="0,6,0,0"/>
      </StackPanel>
      <StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
        <Button x:Name="RefreshButton" Content="刷新列表"/>
        <Button x:Name="PreviewButton" Content="写入预览"/>
        <Button x:Name="CloseButton" Content="安全关闭" Style="{StaticResource DangerButton}"/>
      </StackPanel>
    </Grid>

    <Grid Grid.Row="1" Margin="0,0,0,8">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="*"/>
      </Grid.ColumnDefinitions>
      <Border Style="{StaticResource Card}" Grid.Column="0">
        <StackPanel>
          <TextBlock Text="待处理" Style="{StaticResource MetricLabel}"/>
          <TextBlock x:Name="CandidateCountText" Text="0" Style="{StaticResource MetricValue}"/>
        </StackPanel>
      </Border>
      <Border Style="{StaticResource Card}" Grid.Column="1">
        <StackPanel>
          <TextBlock Text="温和关闭" Style="{StaticResource MetricLabel}"/>
          <TextBlock x:Name="GracefulCountText" Text="0" Style="{StaticResource MetricValue}"/>
        </StackPanel>
      </Border>
      <Border Style="{StaticResource Card}" Grid.Column="2">
        <StackPanel>
          <TextBlock Text="强制清理" Style="{StaticResource MetricLabel}"/>
          <TextBlock x:Name="ForceCountText" Text="0" Style="{StaticResource MetricValue}"/>
        </StackPanel>
      </Border>
      <Border Style="{StaticResource Card}" Grid.Column="3" Margin="0,0,0,12">
        <StackPanel>
          <TextBlock Text="已保护" Style="{StaticResource MetricLabel}"/>
          <TextBlock x:Name="ProtectedCountText" Text="0" Style="{StaticResource MetricValue}"/>
        </StackPanel>
      </Border>
    </Grid>

    <Grid Grid.Row="2">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="2*"/>
        <ColumnDefinition Width="1.05*"/>
      </Grid.ColumnDefinitions>
      <Border Style="{StaticResource Card}" Grid.Column="0">
        <DockPanel>
          <TextBlock DockPanel.Dock="Top" Text="将处理的软件" FontSize="16" FontWeight="SemiBold" Foreground="#1C2524" Margin="0,0,0,10"/>
          <ListView x:Name="CandidateList" BorderThickness="0" Background="#FFFFFF">
            <ListView.View>
              <GridView>
                <GridViewColumn Header="进程" Width="170" DisplayMemberBinding="{Binding Process}"/>
                <GridViewColumn Header="数量" Width="54" DisplayMemberBinding="{Binding Count}"/>
                <GridViewColumn Header="动作" Width="86" DisplayMemberBinding="{Binding Action}"/>
                <GridViewColumn Header="说明" Width="260" DisplayMemberBinding="{Binding Note}"/>
              </GridView>
            </ListView.View>
          </ListView>
        </DockPanel>
      </Border>
      <Border Style="{StaticResource Card}" Grid.Column="1" Margin="0,0,0,12">
        <DockPanel>
          <TextBlock DockPanel.Dock="Top" Text="保护中的软件" FontSize="16" FontWeight="SemiBold" Foreground="#1C2524" Margin="0,0,0,10"/>
          <ListView x:Name="ProtectedList" BorderThickness="0" Background="#FFFFFF">
            <ListView.View>
              <GridView>
                <GridViewColumn Header="进程" Width="170" DisplayMemberBinding="{Binding Process}"/>
                <GridViewColumn Header="数量" Width="54" DisplayMemberBinding="{Binding Count}"/>
              </GridView>
            </ListView.View>
          </ListView>
        </DockPanel>
      </Border>
    </Grid>

    <Border Grid.Row="3" Style="{StaticResource Card}" Margin="0,0,0,0">
      <Grid>
        <Grid.RowDefinitions>
          <RowDefinition Height="Auto"/>
          <RowDefinition Height="130"/>
        </Grid.RowDefinitions>
        <TextBlock Text="运行日志" FontSize="16" FontWeight="SemiBold" Foreground="#1C2524" Margin="0,0,0,10"/>
        <TextBox x:Name="LogBox"
                 Grid.Row="1"
                 IsReadOnly="True"
                 TextWrapping="Wrap"
                 VerticalScrollBarVisibility="Auto"
                 BorderBrush="#DDD7CE"
                 Background="#FBFAF7"
                 Foreground="#24302F"
                 FontFamily="Consolas, Microsoft YaHei UI"
                 FontSize="13"
                 Padding="10"/>
      </Grid>
    </Border>
  </Grid>
</Window>
"@

    $reader = New-Object System.Xml.XmlNodeReader $xaml
    $window = [Windows.Markup.XamlReader]::Load($reader)

    $candidateList = $window.FindName('CandidateList')
    $protectedList = $window.FindName('ProtectedList')
    $candidateCountText = $window.FindName('CandidateCountText')
    $gracefulCountText = $window.FindName('GracefulCountText')
    $forceCountText = $window.FindName('ForceCountText')
    $protectedCountText = $window.FindName('ProtectedCountText')
    $logBox = $window.FindName('LogBox')
    $refreshButton = $window.FindName('RefreshButton')
    $previewButton = $window.FindName('PreviewButton')
    $closeButton = $window.FindName('CloseButton')

    $script:CurrentPlan = $null

    $loadPlan = {
        try {
            $script:CurrentPlan = Get-ClosePlan
            $candidateRows = @(Group-PlanRows -Records $script:CurrentPlan.Candidates)
            $protectedRows = @(Group-PlanRows -Records $script:CurrentPlan.Protected)
            $candidateList.ItemsSource = $candidateRows
            $protectedList.ItemsSource = @($protectedRows | Select-Object -First 80)

            $candidateCountText.Text = [string]@($script:CurrentPlan.Candidates).Count
            $gracefulCountText.Text = [string]@($script:CurrentPlan.Candidates | Where-Object { $_.Action -eq '温和关闭' }).Count
            $forceCountText.Text = [string]@($script:CurrentPlan.Candidates | Where-Object { $_.Action -eq '强制清理' }).Count
            $protectedCountText.Text = [string]@($script:CurrentPlan.Protected).Count
            Add-UiLog -TextBox $logBox -Message ("已刷新：{0}；{1}" -f (Get-Date -Format 'HH:mm:ss'), (Get-ActionSummary -Records $script:CurrentPlan.Candidates))
        }
        catch {
            [System.Windows.MessageBox]::Show($_.Exception.Message, '读取失败', 'OK', 'Error') | Out-Null
        }
    }

    $refreshButton.Add_Click({
        & $loadPlan
    })

    $previewButton.Add_Click({
        if (-not $script:CurrentPlan) {
            & $loadPlan
        }
        Add-UiLog -TextBox $logBox -Message ''
        Add-UiLog -TextBox $logBox -Message (Format-PlanText -Plan $script:CurrentPlan)
    })

    $closeButton.Add_Click({
        if (-not $script:CurrentPlan) {
            & $loadPlan
        }

        $summary = "将处理 {0} 个进程。确认后会先温和关闭，只有白名单后台进程会强制结束。" -f @($script:CurrentPlan.Candidates).Count
        $choice = [System.Windows.MessageBox]::Show($summary, '确认安全关闭', 'YesNo', 'Warning')
        if ($choice -ne 'Yes') {
            Add-UiLog -TextBox $logBox -Message '已取消。'
            return
        }

        $closeButton.IsEnabled = $false
        $refreshButton.IsEnabled = $false
        $previewButton.IsEnabled = $false
        try {
            Invoke-ClosePlan -Plan $script:CurrentPlan -Logger { param([string]$Message) Add-UiLog -TextBox $logBox -Message $Message }
            & $loadPlan
        }
        catch {
            Add-UiLog -TextBox $logBox -Message ("执行失败：{0}" -f $_.Exception.Message)
        }
        finally {
            $closeButton.IsEnabled = $true
            $refreshButton.IsEnabled = $true
            $previewButton.IsEnabled = $true
        }
    })

    $window.Add_Loaded({
        & $loadPlan
    })

    if ($ValidateOnly) {
        Write-Host 'GUI validation passed.'
        return
    }

    [void]$window.ShowDialog()
}

if ($Preview) {
    Show-CliPreview
    if (-not $NoPause) {
        Write-Host ''
        Read-Host '按回车退出' | Out-Null
    }
    exit
}

if ($Run) {
    Invoke-CliRun
    if (-not $NoPause) {
        Write-Host ''
        Read-Host '按回车退出' | Out-Null
    }
    exit
}

if ($ValidateGui) {
    Show-Gui -ValidateOnly
    exit
}

if ([System.Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    $powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    Start-Process -FilePath $powershell -ArgumentList @('-NoProfile', '-STA', '-ExecutionPolicy', 'Bypass', '-File', $MyInvocation.MyCommand.Path)
    exit
}

Show-Gui
