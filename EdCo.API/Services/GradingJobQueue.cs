using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EdCo.Core.Interfaces;

namespace EdCo.API.Services
{
    public class GradingJobQueue : IGradingJobQueue
    {
        private readonly Channel<GradingJobItem> _queue;
        private readonly ICacheService _cacheService;

        public GradingJobQueue(ICacheService cacheService)
        {
            _cacheService = cacheService;
            var options = new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            };
            _queue = Channel.CreateUnbounded<GradingJobItem>(options);
        }

        public async ValueTask EnqueueJobAsync(GradingJobItem job)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));
            
            await SaveJobStatusAsync(job);
            await _queue.Writer.WriteAsync(job);
        }

        public async ValueTask<GradingJobItem?> DequeueJobAsync(CancellationToken cancellationToken)
        {
            return await _queue.Reader.ReadAsync(cancellationToken);
        }

        public async Task SaveJobStatusAsync(GradingJobItem job)
        {
            var cacheKey = $"grading_job:{job.JobId}";
            await _cacheService.SetAsync(cacheKey, job, TimeSpan.FromHours(24));
        }

        public async Task<GradingJobItem?> GetJobStatusAsync(string jobId)
        {
            var cacheKey = $"grading_job:{jobId}";
            return await _cacheService.GetAsync<GradingJobItem>(cacheKey);
        }
    }
}
