namespace LetterTemplatePractice.Models
{
    public class AiQueueOptions
    {
        public int WorkerConcurrency { get; set; } = 1;
        public int MaxAttempts { get; set; } = 5;
        public int BaseDelaySeconds { get; set; } = 30;   // 429 needs long waits on free tier
        public int MaxBackoffSeconds { get; set; } = 300; // cap at 5 minutes
        public int PollIntervalMs { get; set; } = 5000;   // poll every 5s when queue is empty
        public int JitterMs { get; set; } = 2000;
        public int RateLimitBackoffSeconds { get; set; } = 60; // extra delay specifically for 429
    }
}
