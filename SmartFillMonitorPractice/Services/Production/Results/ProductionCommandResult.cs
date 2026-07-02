namespace SmartFillMonitor.Services.Production
{
    public sealed class ProductionCommandResult
    {
        private ProductionCommandResult(bool succeeded, bool commandSent, ProductionRunState state, string message)
        {
            Succeeded = succeeded;
            CommandSent = commandSent;
            State = state;
            Message = message;
        }

        public bool Succeeded { get; }

        public bool CommandSent { get; }

        public ProductionRunState State { get; }

        public string Message { get; }

        public static ProductionCommandResult Success(ProductionRunState state, string message)
        {
            return new ProductionCommandResult(true, true, state, message);
        }

        public static ProductionCommandResult Skipped(ProductionRunState state, string message)
        {
            return new ProductionCommandResult(false, false, state, message);
        }

        public static ProductionCommandResult Failed(ProductionRunState state, string message)
        {
            return new ProductionCommandResult(false, true, state, message);
        }
    }
}
