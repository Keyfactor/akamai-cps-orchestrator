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

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models
{
    // get the CSR generated for an enrollment change
    public class PendingChange
    {
        public PendingCSR[] csrs;
    }

    public class PendingCSR
    {
        public string csr;
        public string keyAlgorithm;
    }
}
