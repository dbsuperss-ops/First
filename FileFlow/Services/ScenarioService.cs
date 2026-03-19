using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FileFlow.Models;
namespace FileFlow.Services
{
    public static class ScenarioService
    {
        private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileFlow");
        private static readonly string FilePath = Path.Combine(Folder, "scenarios.json");
        private static readonly JsonSerializerOptions JsonOpt = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

        public static List<Scenario> Load()
        {
            try { if (!File.Exists(FilePath)) return GetDefault(); return JsonSerializer.Deserialize<List<Scenario>>(File.ReadAllText(FilePath), JsonOpt) ?? GetDefault(); }
            catch (Exception ex) { ErrorService.Report(ex, "ScenarioService.Load"); return GetDefault(); }
        }

        public static bool Save(List<Scenario> scenarios)
        {
            try { Directory.CreateDirectory(Folder); File.WriteAllText(FilePath, JsonSerializer.Serialize(scenarios, JsonOpt)); return true; }
            catch (Exception ex) { ErrorService.Report(ex, "ScenarioService.Save"); return false; }
        }

        public static List<Scenario> GetDefault() => new()
        {
            new Scenario { Name = "기본 분류", IsActive = true, SourceFolder = @"C:\Users\Downloads", TargetFolder = @"C:\Users\Organized", IncludeSubfolders = false, ExcludeSystemFiles = true, ConflictMode = ConflictMode.Rename, Rules = new() { new ClassifyRule { RuleName = "이미지 파일", ConditionOperator = "OR", TargetPath = "이미지", Conditions = new() { new FileCondition { Type = "Extension", Operator = "Equals", Value = ".jpg,.jpeg,.png,.gif,.bmp,.webp" } } }, new ClassifyRule { RuleName = "문서 파일", ConditionOperator = "OR", TargetPath = "문서", Conditions = new() { new FileCondition { Type = "Extension", Operator = "Equals", Value = ".doc,.docx,.pdf,.txt,.xlsx,.pptx,.hwp" } } } } }
        };
    }
}
