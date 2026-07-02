namespace SmartFillMonitor.Services.Production
{
    public sealed class ProductionCaptureResult
    {
        private ProductionCaptureResult(bool saved, string batchNo, string message)
        {
            Saved = saved;
            BatchNo = batchNo;
            Message = message;
        }

        public bool Saved { get; }

        public string BatchNo { get; }

        public string Message { get; }

        public static ProductionCaptureResult SavedRecord(string batchNo)
        {
            return new ProductionCaptureResult(true, batchNo, "生产记录已保存。");
        }

        public static ProductionCaptureResult Skipped(string message)
        {
            return new ProductionCaptureResult(false, string.Empty, message);
        }
    }
}
