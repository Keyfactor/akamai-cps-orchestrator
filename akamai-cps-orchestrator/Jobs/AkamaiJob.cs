// Copyright 2023 Keyfactor
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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