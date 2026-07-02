using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SmartFillMonitor.Models;
using SmartFillMonitor.Services;
using SmartFillMonitor.Services.Production;
using SmartFillMonitor.Services.Security;

namespace SmartFillMonitor.ViewModels.Main
{
    public partial class DashQueryViewModel : ObservableObject
    {
        private readonly IProductionRecordService _productionRecordService;
        private readonly IAuthorizationService _authorizationService;

        [ObservableProperty]
        private ObservableCollection<ProductionRecord> records = new();

        [ObservableProperty]
        private ProductionRecord? selectedRecord;

        [ObservableProperty]
        private DateTime? startDate = DateTime.Today.AddDays(-7);

        [ObservableProperty]
        private DateTime? endDate = DateTime.Today;

        public DashQueryViewModel(IProductionRecordService productionRecordService, IAuthorizationService authorizationService)
        {
            _productionRecordService = productionRecordService;
            _authorizationService = authorizationService;
        }

        [RelayCommand]
        private async Task QueryAsync()
        {
            var start = StartDate ?? DateTime.Today.AddDays(-7);
            var end = EndDate ?? DateTime.Today;
            var endInclusive = end.AddDays(1).AddTicks(-1);
            var result = await _productionRecordService.QueryAsync(start, endInclusive);
            var sorted = result.OrderByDescending(x => x.Time).ToList();

            Records.Clear();
            foreach (var item in sorted)
            {
                Records.Add(item);
            }
        }

        [RelayCommand]
        private async Task ExportAsync()
        {
            if (Records.Count == 0)
            {
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "CSV 文件|*.csv",
                FileName = $"ProductionRecords_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _authorizationService.EnsurePermission(Permission.ExportLogs, "导出生产记录");
                await _productionRecordService.ExportAsync(Records.ToList(), dialog.FileName);
            }
            catch (Exception ex)
            {
                LogHelper.Error("导出生产记录失败", ex);
            }
        }
    }
}
