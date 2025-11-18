using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModelingEvolution.VideoStreaming.CVat;

public class CVatClient : ICVatClient
{
    private readonly HttpClient _httpClient;

    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly string _url;

    public CVatClient(string url, string token)
    {
        _url = url;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", token);
        _httpClient.BaseAddress = new Uri(url);
    }


    public async Task<TaskListResponse> ListTasks(
        string assignee = null,
        string dimension = null,
        string filter = null,
        string mode = null,
        string name = null,
        string org = null,
        int? orgId = null,
        string owner = null,
        int? page = null,
        int? pageSize = null,
        int? projectId = null,
        string projectName = null,
        string search = null,
        string sort = null,
        string status = null,
        string subset = null,
        string trackerLink = null,
        string xOrganization = null)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrEmpty(assignee)) queryParams.Add($"assignee={assignee}");
        if (!string.IsNullOrEmpty(dimension)) queryParams.Add($"dimension={dimension}");
        if (!string.IsNullOrEmpty(filter)) queryParams.Add($"filter={filter}");
        if (!string.IsNullOrEmpty(mode)) queryParams.Add($"mode={mode}");
        if (!string.IsNullOrEmpty(name)) queryParams.Add($"name={name}");
        if (!string.IsNullOrEmpty(org)) queryParams.Add($"org={org}");
        if (orgId.HasValue) queryParams.Add($"org_id={orgId}");
        if (!string.IsNullOrEmpty(owner)) queryParams.Add($"owner={owner}");
        if (page.HasValue) queryParams.Add($"page={page}");
        if (pageSize.HasValue) queryParams.Add($"page_size={pageSize}");
        if (projectId.HasValue) queryParams.Add($"project_id={projectId}");
        if (!string.IsNullOrEmpty(projectName)) queryParams.Add($"project_name={projectName}");
        if (!string.IsNullOrEmpty(search)) queryParams.Add($"search={search}");
        if (!string.IsNullOrEmpty(sort)) queryParams.Add($"sort={sort}");
        if (!string.IsNullOrEmpty(status)) queryParams.Add($"status={status}");
        if (!string.IsNullOrEmpty(subset)) queryParams.Add($"subset={subset}");
        if (!string.IsNullOrEmpty(trackerLink)) queryParams.Add($"tracker_link={trackerLink}");
        var queryString = string.Join("&", queryParams);
        var requestUri = $"{_url}/tasks";
        if (!string.IsNullOrEmpty(queryString)) requestUri += $"?{queryString}";
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        if (!string.IsNullOrEmpty(xOrganization)) request.Headers.Add("X-Organization", xOrganization);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadAsStringAsync();

        var taskListResponse = JsonSerializer.Deserialize<TaskListResponse>(data, _options);
        return taskListResponse;
    }

    public async Task<CvatTaskResponse> CreateTask(
        string name,
        string subset,
        int projectId,
        int? ownerId = null,
        int? assigneeId = null,
        string bugTracker = null,
        int? overlap = null,
        int segmentSize = 0,
        List<LabelRequest> labels = null,
        Storage targetStorage = null,
        Storage sourceStorage = null,
        string org = null,
        int? orgId = null,
        string xOrganization = null)
    {
        var requestBody = new CreateTaskRequest
        {
            Name = name,
            ProjectId = projectId,
            OwnerId = ownerId,
            AssigneeId = assigneeId,
            BugTracker = bugTracker,
            Overlap = overlap,
            SegmentSize = segmentSize,
            Labels = labels ?? new List<LabelRequest>(),
            Subset = subset,
            TargetStorage = targetStorage,
            SourceStorage = sourceStorage
        };
        var queryParams = new List<string>();
        if (!string.IsNullOrEmpty(org)) queryParams.Add($"org={org}");
        if (orgId.HasValue) queryParams.Add($"org_id={orgId}");
        var queryString = string.Join("&", queryParams);
        var requestUri = $"{_url}/tasks";
        if (!string.IsNullOrEmpty(queryString)) requestUri += $"?{queryString}";

        var json = JsonSerializer.Serialize(requestBody, _options);
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(xOrganization)) request.Headers.Add("X-Organization", xOrganization);
        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        if (response.IsSuccessStatusCode)
        {
            var data = content;
            return JsonSerializer.Deserialize<CvatTaskResponse>(data, _options);
        }

        throw new Exception(content);
    }


    public async Task<string> AttachTaskData(int taskId, int imageQuality, bool copyData, params string[] urls)
    {
        var req = new AttachTaskDataRequest
        {
            Storage = "local",
            ImageQuality = imageQuality,
            CopyData = copyData,
            RemoteFiles = urls.ToList()
        };
        var json = JsonSerializer.Serialize(req, _options);
        var response = await _httpClient.PostAsync($"{_url}/tasks/{taskId}/data",
            new StringContent(json, Encoding.UTF8, "application/json"));
        var content = await response.Content.ReadAsStringAsync();
        if (response.IsSuccessStatusCode)
            return content;
        throw new Exception(content);
    }


    public void Dispose()
    {
        _httpClient.Dispose();
    }
}