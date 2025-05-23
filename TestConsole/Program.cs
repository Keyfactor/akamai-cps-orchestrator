// Copyright 2025 Keyfactor
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

using System.Diagnostics;
using Keyfactor.Extensions.Utilities.HttpInterface;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models;
using Microsoft.Extensions.Logging;

namespace TestConsole;

class Program
{
    private static ILogger<Program> _logger;
    
    public static void Main(string[] args)
    {
        string clientSecret = GetEnvironmentVariable("AKAMAI_CLIENT_SECRET");
        string clientToken = GetEnvironmentVariable("AKAMAI_CLIENT_TOKEN");
        string accessToken = GetEnvironmentVariable("AKAMAI_ACCESS_TOKEN");
        
        string clientMachine = GetEnvironmentVariable("AKAMAI_HOST");
        
        Dictionary<string, string> storeProps = new()
        {
            { "client_secret", clientSecret },
            { "client_token", clientToken },
            { "access_token", accessToken }
        };
        
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole()
                .SetMinimumLevel(LogLevel.Debug); // Set minimum log level here
        });
        
        _logger = loggerFactory.CreateLogger<Program>();
        
        AkamaiAuth auth = new AkamaiAuth(storeProps);
        AkamaiClient client = new AkamaiClient(_logger, clientMachine, auth);
        client.SetDeploymentType(Constants.StorePaths.Staging);
        
        // StressTestGetEnrollments(client);
        
        var enrollments = GetEnrollments(client);
        var enrollment = enrollments.FirstOrDefault();
        //
        if (enrollment == null)
        {
            _logger.LogWarning("No enrollments found in system. Unable to perform certificate / enrollment actions");
            return;
        }
        
        GetEnrollment(client, enrollment.id);
        GetCertificate(client, enrollment.id);
        // GetEnrollmentChangeHistory(client, enrollment.id);
        UpdateEnrollment(client, enrollment.id, enrollment);
        
        _logger.LogInformation("API test suite completed successfully.");
    }

    private static void StressTestGetEnrollments(AkamaiClient client)
    {
        int maxRequests = 51;
        // https://techdocs.akamai.com/cps/reference/rate-limiting
        // According to the docs here, we should be rate limited 50 requests per minute
        // or 20 requests per 2 seconds.
        for (int i = 1; i <= maxRequests; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation($"Sending request {i} of {maxRequests}...");
            client.GetEnrollments();
            stopwatch.Stop();
            _logger.LogInformation($"Completed request {i} of {maxRequests}. Time elapsed (ms): {stopwatch.ElapsedMilliseconds}");
        }
    }
    
    private static Enrollment[] GetEnrollments(AkamaiClient client)
    {
        _logger.LogInformation($"Getting enrollments...");
        var result = client.GetEnrollments();
        _logger.LogInformation($"Enrollments retrieved successfully. Count: {result.Length}");
        return result;
    }

    private static Enrollment GetEnrollment(AkamaiClient client, string id)
    {
        _logger.LogInformation($"Getting enrollment {id}...");
        var enrollment = client.GetEnrollment(id);
        _logger.LogInformation($"Enrollment {id} retrieved successfully.");
        return enrollment;
    }
    
    private static void GetCertificate(AkamaiClient client, string enrollmentId)
    {
        _logger.LogInformation($"Getting certificate with enrollment ID {enrollmentId}...");
        var cert = client.GetCertificate(enrollmentId);
        _logger.LogInformation($"Certificate with enrollment ID {enrollmentId} retrieved successfully.");
    }

    private static void GetEnrollmentChangeHistory(AkamaiClient client, string enrollmentId)
    {
        _logger.LogInformation($"Getting enrollment change history for enrollment ID {enrollmentId}...");
        var history = client.GetEnrollmentChangeHistory(enrollmentId);
        _logger.LogInformation($"Enrollment change history for enrollment ID {enrollmentId} retrieved successfully. Change history count: {history.changes.Length}");
    }

    private static void UpdateEnrollment(AkamaiClient client, string enrollmentId, Enrollment enrollment)
    {
        _logger.LogInformation($"Updating enrollment {enrollmentId}...");
        client.UpdateEnrollment(enrollmentId, enrollment);
        _logger.LogInformation($"Enrollment {enrollmentId} updated successfully.");
    }

    private static string GetEnvironmentVariable(string env)
    {
        string? value = Environment.GetEnvironmentVariable(env);
        
        if (!string.IsNullOrWhiteSpace(value)) return value;
        
        throw new ArgumentException($"Environment variable {env} is required for test console");
    }
}
