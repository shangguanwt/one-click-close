using Microsoft.UI.Xaml.Media;
using OneClickClose.Core;
using OneClickClose.WinUI.Helpers;

namespace OneClickClose.WinUI.ViewModels;

public class CandidateRowViewModel
{
    public string Process { get; }
    public string Note { get; }
    public string Action { get; }
    public SolidColorBrush ActionColor { get; }
    public string Count { get; }
    public string MemoryText { get; }
    public string RiskText { get; }
    public SolidColorBrush RiskColor { get; }
    public bool IsHighRisk { get; }
    public double Opacity { get; }
    public string ProcessNameRaw { get; }

    public CandidateRowViewModel(ProcessGroupRow row)
    {
        Process = row.Process ?? "";
        ProcessNameRaw = row.Process ?? "";
        Note = row.Note ?? "";
        Action = row.Action ?? "";
        Count = row.Count.ToString();
        MemoryText = row.MemoryMb > 0 ? row.MemoryMb + " MB" : "-";
        RiskText = row.RiskScore + "";
        IsHighRisk = row.IsHighRisk;

        ActionColor = ColorHelper.GetActionBackground(row.Action);

        if (row.IsHighRisk)
            RiskColor = ColorHelper.Danger;
        else if (row.RiskScore >= 40)
            RiskColor = ColorHelper.Copper;
        else
            RiskColor = ColorHelper.Safe;

        Opacity = row.IsHighRisk ? 0.7 : 1.0;
    }
}
