
using System;
using Keyfactor.Orchestrators.Extensions;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Jobs
{
    public abstract class AkamaiJob
    {
        public long JobHistoryId { get; set; }

        public JobResult Success()
        {
            return new JobResult() {
                JobHistoryId = JobHistoryId,
                Result = Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Success
            };
        }

        public JobResult Warning(string warnMessage)
        {
            return new JobResult() {
                JobHistoryId = JobHistoryId,
                Result = Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Warning,
                FailureMessage = warnMessage
            };
        }
        
        public JobResult Failure(string errorMessage)
        {
            return new JobResult() {
                JobHistoryId = JobHistoryId,
                Result = Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Failure,
                FailureMessage = errorMessage
            };
        }

        public JobResult Failure(Exception exception)
        {
            return Failure(FlattenException(exception));
        }

        public string FlattenException(Exception exception)
        {
            string message = exception.Message;
            if (exception.InnerException != null)
            {
                message += "\n\t--- Inner exception: ---";
                message += "\n\t " + FlattenException(exception.InnerException);
            }

            return message;
        }
    }
}