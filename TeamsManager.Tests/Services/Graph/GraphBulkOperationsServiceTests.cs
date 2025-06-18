using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using TeamsManager.Core.Services.Graph;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Models.Graph;
using TeamsManager.Core.Exceptions.Graph;
using System.Text.Json;

namespace TeamsManager.Tests.Services.Graph
{
    public class GraphBulkOperationsServiceTests : IDisposable
    {
        private readonly Mock<IModernHttpService> _mockHttpService;
        private readonly Mock<IGraphConnectionService> _mockConnectionService;
        private readonly Mock<ILogger<GraphBulkOperationsService>> _mockLogger;
        private readonly GraphApiConfiguration _graphConfig;
        private readonly GraphBulkOperationsService _service;

        public GraphBulkOperationsServiceTests()
        {
            _mockHttpService = new Mock<IModernHttpService>();
            _mockConnectionService = new Mock<IGraphConnectionService>();
            _mockLogger = new Mock<ILogger<GraphBulkOperationsService>>();
            
            _graphConfig = new GraphApiConfiguration
            {
                BaseUrl = "https://graph.microsoft.com/v1.0",
                Endpoints = new GraphEndpoints()
            };

            _service = new GraphBulkOperationsService(
                _mockHttpService.Object,
                _mockConnectionService.Object,
                _mockLogger.Object,
                _graphConfig);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange & Act
            var service = new GraphBulkOperationsService(
                _mockHttpService.Object,
                _mockConnectionService.Object,
                _mockLogger.Object,
                _graphConfig);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public void Constructor_WithNullHttpService_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GraphBulkOperationsService(
                null,
                _mockConnectionService.Object,
                _mockLogger.Object,
                _graphConfig));
        }

        [Fact]
        public void Constructor_WithNullConnectionService_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GraphBulkOperationsService(
                _mockHttpService.Object,
                null,
                _mockLogger.Object,
                _graphConfig));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GraphBulkOperationsService(
                _mockHttpService.Object,
                _mockConnectionService.Object,
                null,
                _graphConfig));
        }

        [Fact]
        public void Constructor_WithNullGraphConfig_UsesDefaultConfiguration()
        {
            // Arrange & Act
            var service = new GraphBulkOperationsService(
                _mockHttpService.Object,
                _mockConnectionService.Object,
                _mockLogger.Object,
                null);

            // Assert
            Assert.NotNull(service);
        }

        #endregion

        #region BulkAddUsersToTeamAsync Tests

        [Fact]
        public async Task BulkAddUsersToTeamAsync_WithValidParameters_ReturnsSuccessfulResults()
        {
            // Arrange
            var teamId = "team-123";
            var userUpns = new List<string> { "user1@test.com", "user2@test.com" };
            var role = "Member";

            SetupMockServices();
            SetupBatchPostMock(new List<(string Id, int Status)> 
            { 
                ("", 201), ("", 201) // ID will be dynamically matched
            });

            var progress = new Mock<IProgress<BulkOperationProgress>>();

            // Act
            var result = await _service.BulkAddUsersToTeamAsync(teamId, userUpns, role, progress.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.True(result["user1@test.com"]);
            Assert.True(result["user2@test.com"]);
        }

        [Fact]
        public async Task BulkAddUsersToTeamAsync_WithEmptyTeamId_ThrowsArgumentException()
        {
            // Arrange
            var userUpns = new List<string> { "user1@test.com" };
            var progress = new Mock<IProgress<BulkOperationProgress>>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.BulkAddUsersToTeamAsync("", userUpns, "Member", progress.Object));
        }

        [Fact]
        public async Task BulkAddUsersToTeamAsync_WithEmptyUserList_ThrowsArgumentException()
        {
            // Arrange
            var teamId = "team-123";
            var userUpns = new List<string>();
            var progress = new Mock<IProgress<BulkOperationProgress>>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.BulkAddUsersToTeamAsync(teamId, userUpns, "Member", progress.Object));
        }

        [Fact]
        public async Task BulkAddUsersToTeamAsync_WithProgressReporter_ReportsProgress()
        {
            // Arrange
            var teamId = "team-123";
            var userUpns = new List<string> { "user1@test.com" };
            
            SetupMockServices();
            SetupBatchPostMock(new List<(string Id, int Status)> { ("", 201) });

            var progressReports = new List<BulkOperationProgress>();
            var progress = new Progress<BulkOperationProgress>(report => progressReports.Add(report));

            // Act
            await _service.BulkAddUsersToTeamAsync(teamId, userUpns, "Member", progress);

            // Assert
            Assert.True(progressReports.Count > 0);
            Assert.Contains(progressReports, p => p.TotalOperations == userUpns.Count);
        }

        [Fact]
        public async Task BulkAddUsersToTeamAsync_WithGraphConnectionException_HandlesException()
        {
            // Arrange
            var teamId = "team-123";
            var userUpns = new List<string> { "user1@test.com" };

            _mockConnectionService.Setup(x => x.EnsureValidTokenAsync())
                .ThrowsAsync(new GraphConnectionException("Connection failed"));

            var progress = new Mock<IProgress<BulkOperationProgress>>();

            // Act
            var result = await _service.BulkAddUsersToTeamAsync(teamId, userUpns, "Member", progress.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Count);
            Assert.False(result["user1@test.com"]);
        }

        #endregion

        #region BulkRemoveUsersFromTeamAsync Tests

        [Fact]
        public async Task BulkRemoveUsersFromTeamAsync_WithValidParameters_ReturnsSuccessfulResults()
        {
            // Arrange
            var teamId = "team-123";
            var userUpns = new List<string> { "user1@test.com", "user2@test.com" };

            SetupMockServices();
            SetupTeamMembersResponse(teamId, userUpns);
            SetupSuccessfulBatchResponse();

            var progress = new Mock<IProgress<BulkOperationProgress>>();

            // Act
            var result = await _service.BulkRemoveUsersFromTeamAsync(teamId, userUpns, progress.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.True(result["user1@test.com"]);
            Assert.True(result["user2@test.com"]);
        }

        [Fact]
        public async Task BulkRemoveUsersFromTeamAsync_WithEmptyTeamId_ThrowsArgumentException()
        {
            // Arrange
            var userUpns = new List<string> { "user1@test.com" };
            var progress = new Mock<IProgress<BulkOperationProgress>>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.BulkRemoveUsersFromTeamAsync("", userUpns, progress.Object));
        }

        [Fact]
        public async Task BulkRemoveUsersFromTeamAsync_WithEmptyUserList_ThrowsArgumentException()
        {
            // Arrange
            var teamId = "team-123";
            var userUpns = new List<string>();
            var progress = new Mock<IProgress<BulkOperationProgress>>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.BulkRemoveUsersFromTeamAsync(teamId, userUpns, progress.Object));
        }

        [Fact]
        public async Task BulkRemoveUsersFromTeamAsync_WithNonMemberUser_ReturnsFalseForUser()
        {
            // Arrange
            var teamId = "team-123";
            var userUpns = new List<string> { "nonmember@test.com" };

            SetupMockServices();
            SetupTeamMembersResponse(teamId, new List<string>()); // No members

            var progress = new Mock<IProgress<BulkOperationProgress>>();

            // Act
            var result = await _service.BulkRemoveUsersFromTeamAsync(teamId, userUpns, progress.Object);

            // Assert
            Assert.False(result["nonmember@test.com"]);
        }

        #endregion

        #region BulkArchiveTeamsAsync Tests

        [Fact]
        public async Task BulkArchiveTeamsAsync_WithValidTeamIds_ReturnsSuccessfulResults()
        {
            // Arrange
            var teamIds = new List<string> { "team-1", "team-2" };

            SetupMockServices();
            SetupBatchPostMock(new List<(string Id, int Status)> { ("", 201), ("", 201) });

            var progress = new Mock<IProgress<BulkOperationProgress>>();

            // Act
            var result = await _service.BulkArchiveTeamsAsync(teamIds, progress.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.True(result["team-1"]);
            Assert.True(result["team-2"]);
        }

        [Fact]
        public async Task BulkArchiveTeamsAsync_WithEmptyTeamList_ThrowsArgumentException()
        {
            // Arrange
            var teamIds = new List<string>();
            var progress = new Mock<IProgress<BulkOperationProgress>>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.BulkArchiveTeamsAsync(teamIds, progress.Object));
        }

        [Fact]
        public async Task BulkArchiveTeamsAsync_WithProgressReporter_ReportsProgress()
        {
            // Arrange
            var teamIds = new List<string> { "team-1" };
            
            SetupMockServices();
            SetupBatchPostMock(new List<(string Id, int Status)> { ("", 201) });

            var progressReports = new List<BulkOperationProgress>();
            var progress = new Progress<BulkOperationProgress>(report => progressReports.Add(report));

            // Act
            await _service.BulkArchiveTeamsAsync(teamIds, progress);

            // Assert
            Assert.True(progressReports.Count > 0);
            Assert.Contains(progressReports, p => p.TotalOperations == teamIds.Count);
        }

        #endregion

        #region ExecuteBatchOperationsAsync Tests

        [Fact]
        public async Task ExecuteBatchOperationsAsync_WithValidOperations_ReturnsSuccessfulResult()
        {
            // Arrange
            var operations = new List<GraphBatchOperation>
            {
                GraphBatchOperation.CreateGet("/v1.0/me", "GetUser"),
                GraphBatchOperation.CreatePost("/v1.0/teams/team-1/members", new { roles = new[] { "Member" } }, "AddMember")
            };
            
            var accessToken = "valid-token";

            // Mock IModernHttpService.PostAsync dla /$batch endpoint
            var batchResponse = new
            {
                responses = new[]
                {
                    new { id = operations[0].Id, status = 200, headers = new Dictionary<string, string>(), body = new { id = "result-1" } },
                    new { id = operations[1].Id, status = 201, headers = new Dictionary<string, string>(), body = new { id = "result-2" } }
                }
            };
            
            var jsonResponse = System.Text.Json.JsonSerializer.Serialize(batchResponse);
            var mockHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            };

            _mockHttpService.Setup(x => x.PostAsync(
                It.Is<string>(url => url.Contains("/$batch")),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(mockHttpResponse);

            // Act
            var result = await _service.ExecuteBatchOperationsAsync(operations, accessToken);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(2, result.BatchResults.Count);
            Assert.True(result.BatchResults.All(r => r.IsSuccessful));
        }

        [Fact]
        public async Task ExecuteBatchOperationsAsync_WithEmptyOperations_ReturnsEmptyResult()
        {
            // Arrange
            var operations = new List<GraphBatchOperation>();
            var accessToken = "valid-token";

            // Act
            var result = await _service.ExecuteBatchOperationsAsync(operations, accessToken);

            // Assert
            Assert.True(result.Success);
            Assert.Empty(result.BatchResults);
        }

        [Fact]
        public async Task ExecuteBatchOperationsAsync_WithNullAccessToken_ThrowsArgumentException()
        {
            // Arrange
            var operations = new List<GraphBatchOperation>
            {
                GraphBatchOperation.CreateGet("/v1.0/me")
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.ExecuteBatchOperationsAsync(operations, null));
        }

        [Fact]
        public async Task ExecuteBatchOperationsAsync_WithLargeBatch_ChunksOperations()
        {
            // Arrange - Create 25 operations (exceeds max batch size of 20)
            var operations = Enumerable.Range(1, 25)
                .Select(i => GraphBatchOperation.CreateGet($"/v1.0/users/{i}", "GetUser"))
                .ToList();
            
            var accessToken = "valid-token";

            // Mock IModernHttpService.PostAsync dla /$batch endpoint - chunking będzie wykonywać multiple calls
            _mockHttpService.Setup(x => x.PostAsync(
                It.Is<string>(url => url.Contains("/$batch")),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync((string url, string requestBody, Dictionary<string, string> headers) =>
                {
                    // Parse request to get actual IDs and return success for all
                    var requestData = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(requestBody);
                    var actualResponses = new List<object>();

                    if (requestData.TryGetProperty("requests", out var requestsArray))
                    {
                        foreach (var request in requestsArray.EnumerateArray())
                        {
                            if (request.TryGetProperty("id", out var idElement))
                            {
                                var actualId = idElement.GetString();
                                actualResponses.Add(new
                                {
                                    id = actualId,
                                    status = 200,
                                    headers = new Dictionary<string, string>(),
                                    body = new { id = $"result-{actualId}" }
                                });
                            }
                        }
                    }

                    var batchResponse = new { responses = actualResponses.ToArray() };
                    var jsonResponse = System.Text.Json.JsonSerializer.Serialize(batchResponse);
                    
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
                    };
                });

            // Act
            var result = await _service.ExecuteBatchOperationsAsync(operations, accessToken);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(25, result.BatchResults.Count); // Wszystkie 25 operacji powinno być przetworzone
            Assert.True(result.BatchResults.All(r => r.IsSuccessful));
            
            // Verify że PostAsync został wywołany więcej niż raz (chunking)
            _mockHttpService.Verify(x => x.PostAsync(
                It.Is<string>(url => url.Contains("/$batch")),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>()), 
                Times.AtLeast(2)); // Powinno być co najmniej 2 wywołania dla 25 operacji (20+5)
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task BulkArchiveTeamsAsync_WithHttpRequestException_HandlesSomeFailures()
        {
            // Arrange
            var teamIds = new List<string> { "team-1", "team-2" };

            SetupMockServices();

            // Mock IModernHttpService.PostAsync dla /$batch endpoint z mixed results
            var batchResponse = new
            {
                responses = new object[]
                {
                    new { id = "1", status = 204, headers = new Dictionary<string, string>(), body = new { } }, // Success
                    new { id = "2", status = 400, headers = new Dictionary<string, string>(), body = new { error = "Bad Request" } }  // Failure
                }
            };
            
            var jsonResponse = System.Text.Json.JsonSerializer.Serialize(batchResponse);
            var mockHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            };

            _mockHttpService.Setup(x => x.PostAsync(
                It.Is<string>(url => url.Contains("/$batch")),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(mockHttpResponse);

            var progress = new Mock<IProgress<BulkOperationProgress>>();

            // Act
            var result = await _service.BulkArchiveTeamsAsync(teamIds, progress.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            // Note: The actual implementation determines success/failure based on batch response analysis
        }

        #endregion

        #region Rate Limiting Tests

        [Fact]
        public async Task GetRateLimitStatusAsync_ReturnsValidStatus()
        {
            // Arrange
            SetupMockServices();

            // Mock a simple HTTP response for rate limit checking
            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK);
            mockResponse.Headers.Add("X-RateLimit-Remaining", "950");
            mockResponse.Headers.Add("X-RateLimit-Limit", "1000");

            _mockHttpService.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(mockResponse);

            // Act
            var result = await _service.GetRateLimitStatusAsync();

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsLimitReached);
        }

        #endregion

        #region Helper Methods

        private void SetupMockServices()
        {
            _mockConnectionService.Setup(x => x.EnsureValidTokenAsync())
                .ReturnsAsync(true);

            _mockConnectionService.Setup(x => x.GetAccessTokenAsync())
                .ReturnsAsync("valid-access-token");
        }

        private void SetupSuccessfulBatchResponse()
        {
            _mockHttpService.Setup(x => x.PostAsync(
                It.Is<string>(url => url.Contains("/$batch")),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync((string url, string requestBody, Dictionary<string, string> headers) =>
                {
                    // Parse request to get actual IDs and return success for all
                    var requestData = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(requestBody);
                    var actualResponses = new List<object>();

                    if (requestData.TryGetProperty("requests", out var requestsArray))
                    {
                        foreach (var request in requestsArray.EnumerateArray())
                        {
                            if (request.TryGetProperty("id", out var idElement))
                            {
                                var actualId = idElement.GetString();
                                actualResponses.Add(new
                                {
                                    id = actualId,
                                    status = 201,
                                    headers = new Dictionary<string, string>(),
                                    body = new { id = $"result-{actualId}" }
                                });
                            }
                        }
                    }

                    var batchResponse = new { responses = actualResponses.ToArray() };
                    var jsonResponse = System.Text.Json.JsonSerializer.Serialize(batchResponse);
                    
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
                    };
                });
        }

        private void SetupTeamMembersResponse(string teamId, List<string> userUpns)
        {
            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK);
            var membersJson = $$"""
            {
                "value": [
                    {{string.Join(",", userUpns.Select((upn, index) => $$$"""
                    {
                        "id": "member-{{{index}}}",
                        "email": "{{{upn}}}",
                        "userPrincipalName": "{{{upn}}}"
                    }
                    """))}}
                ]
            }
            """;
            
            mockResponse.Content = new StringContent(membersJson);

            _mockHttpService.Setup(x => x.GetAsync(
                It.Is<string>(url => url.Contains($"teams/{teamId}/members")),
                It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(mockResponse);
        }

        private void SetupBatchPostMock(List<(string Id, int Status)> responses)
        {
            _mockHttpService.Setup(x => x.PostAsync(
                It.Is<string>(url => url.Contains("/$batch")),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync((string url, string requestBody, Dictionary<string, string> headers) =>
                {
                    // Parse request to get actual IDs
                    var requestData = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(requestBody);
                    var actualResponses = new List<object>();

                    if (requestData.TryGetProperty("requests", out var requestsArray))
                    {
                        var index = 0;
                        foreach (var request in requestsArray.EnumerateArray())
                        {
                            if (request.TryGetProperty("id", out var idElement))
                            {
                                var actualId = idElement.GetString();
                                var status = index < responses.Count ? responses[index].Status : 201;
                                
                                actualResponses.Add(new
                                {
                                    id = actualId,
                                    status = status,
                                    headers = new Dictionary<string, string>(),
                                    body = new { id = $"result-{actualId}" }
                                });
                                index++;
                            }
                        }
                    }

                    var batchResponse = new { responses = actualResponses.ToArray() };
                    var jsonResponse = System.Text.Json.JsonSerializer.Serialize(batchResponse);
                    
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
                    };
                });
        }

        #endregion

        public void Dispose()
        {
            _mockHttpService?.Reset();
            _mockConnectionService?.Reset();
            _mockLogger?.Reset();
        }
    }
} 