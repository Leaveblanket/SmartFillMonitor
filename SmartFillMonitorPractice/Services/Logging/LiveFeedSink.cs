using System.IO;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace SmartFillMonitor.Services.Logging
{
    public sealed class LiveFeedSink : ILogEventSink
    {
        private readonly ILogLiveFeed _liveFeed;
        private readonly MessageTemplateTextFormatter _formatter;

        public LiveFeedSink(ILogLiveFeed liveFeed, string outputTemplate)
        {
            _liveFeed = liveFeed;
            _formatter = new MessageTemplateTextFormatter(outputTemplate);
        }

        public void Emit(LogEvent logEvent)
        {
            try
            {
                using var writer = new StringWriter();
                _formatter.Format(logEvent, writer);
                var message = writer.ToString().TrimEnd('\r', '\n');
                _liveFeed.Publish(message);
            }
            catch (IOException ex)
            {
                SelfLog.WriteLine("LiveFeedSink failed to format log event: {0}", ex);
            }
            catch (System.Exception ex)
            {
                SelfLog.WriteLine("LiveFeedSink failed to publish live log: {0}", ex);
            }
        }
    }
}
