using System.Text.Json;
using System.Text.Json.Serialization;
using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models.Organize;

namespace DupeFinderPro.Infrastructure.Organize;

public sealed class JsonScenarioRepository : IScenarioRepository
{
    private static readonly string DataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DupeFinderPro");
    private static readonly string FilePath = Path.Combine(DataFolder, "scenarios.json");
    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public IReadOnlyList<Scenario> GetAll()
    {
        try
        {
            if (!File.Exists(FilePath)) return GetDefault();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<ScenarioDto>>(json, JsonOpt)
                       ?.Select(ToModel).ToList()
                   ?? GetDefault();
        }
        catch { return GetDefault(); }
    }

    public bool Save(IReadOnlyList<Scenario> scenarios)
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            var dtos = scenarios.Select(ToDto).ToList();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(dtos, JsonOpt));
            return true;
        }
        catch { return false; }
    }

    public Scenario? GetById(Guid id) => GetAll().FirstOrDefault(s => s.Id == id);

    private static IReadOnlyList<Scenario> GetDefault() =>
    [
        new Scenario(
            Id: Guid.NewGuid(),
            Name: "기본 분류",
            IsActive: true,
            SourceFolder: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            TargetFolder: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Organized"),
            IncludeSubfolders: false,
            ExcludeSystemFiles: true,
            CleanupEmptyFolders: false,
            ConflictMode: ConflictMode.Rename,
            Rules:
            [
                new ClassifyRule("이미지 파일",
                    [new FileCondition(ConditionType.Extension, ConditionOperator.Equals, ".jpg,.jpeg,.png,.gif,.bmp,.webp")],
                    ConditionLogic.Or, "이미지", "", DestinationMode.Default),
                new ClassifyRule("문서 파일",
                    [new FileCondition(ConditionType.Extension, ConditionOperator.Equals, ".doc,.docx,.pdf,.txt,.xlsx,.pptx,.hwp")],
                    ConditionLogic.Or, "문서", "", DestinationMode.Default)
            ],
            IsScheduled: false,
            ScheduleTime: "09:00",
            ScheduleDays: [])
    ];

    // DTO for JSON (flat properties for backward compatibility)
    private sealed class ScenarioDto
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public string SourceFolder { get; set; } = "";
        public string TargetFolder { get; set; } = "";
        public bool IncludeSubfolders { get; set; }
        public bool ExcludeSystemFiles { get; set; } = true;
        public bool CleanupEmptyFolders { get; set; }
        public ConflictMode ConflictMode { get; set; } = ConflictMode.Rename;
        public List<ClassifyRuleDto> Rules { get; set; } = [];
        public bool IsScheduled { get; set; }
        public string ScheduleTime { get; set; } = "09:00";
        public List<string> ScheduleDays { get; set; } = [];
    }

    private sealed class ClassifyRuleDto
    {
        public string RuleName { get; set; } = "";
        public List<FileConditionDto> Conditions { get; set; } = [];
        public ConditionLogic Logic { get; set; } = ConditionLogic.And;
        public string TargetPath { get; set; } = "";
        public string Destination { get; set; } = "";
        public DestinationMode DestinationMode { get; set; } = DestinationMode.Default;
    }

    private sealed class FileConditionDto
    {
        public ConditionType Type { get; set; }
        public ConditionOperator Operator { get; set; }
        public string Value { get; set; } = "";
        public SizeUnit Unit { get; set; } = SizeUnit.Bytes;
    }

    private static Scenario ToModel(ScenarioDto d) => new(
        d.Id, d.Name, d.IsActive, d.SourceFolder, d.TargetFolder,
        d.IncludeSubfolders, d.ExcludeSystemFiles, d.CleanupEmptyFolders,
        d.ConflictMode,
        d.Rules.Select(r => new ClassifyRule(
            r.RuleName,
            r.Conditions.Select(c => new FileCondition(c.Type, c.Operator, c.Value, c.Unit)).ToList(),
            r.Logic, r.TargetPath, r.Destination, r.DestinationMode)).ToList(),
        d.IsScheduled, d.ScheduleTime, d.ScheduleDays);

    private static ScenarioDto ToDto(Scenario s) => new()
    {
        Id = s.Id, Name = s.Name, IsActive = s.IsActive,
        SourceFolder = s.SourceFolder, TargetFolder = s.TargetFolder,
        IncludeSubfolders = s.IncludeSubfolders, ExcludeSystemFiles = s.ExcludeSystemFiles,
        CleanupEmptyFolders = s.CleanupEmptyFolders, ConflictMode = s.ConflictMode,
        Rules = s.Rules.Select(r => new ClassifyRuleDto
        {
            RuleName = r.RuleName, Logic = r.Logic, TargetPath = r.TargetPath,
            Destination = r.Destination, DestinationMode = r.DestinationMode,
            Conditions = r.Conditions.Select(c => new FileConditionDto
                { Type = c.Type, Operator = c.Operator, Value = c.Value, Unit = c.Unit }).ToList()
        }).ToList(),
        IsScheduled = s.IsScheduled, ScheduleTime = s.ScheduleTime,
        ScheduleDays = s.ScheduleDays.ToList()
    };
}
