namespace ModelingEvolution.VideoStreaming.CVat;

public interface ICVatClient : IDisposable
{
    Task<TaskListResponse> ListTasks(
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
        string xOrganization = null);

    Task<CvatTaskResponse> CreateTask(
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
        string xOrganization = null);

    Task<string> AttachTaskData(int taskId, int imageQuality, bool copyData, params string[] urls);
}