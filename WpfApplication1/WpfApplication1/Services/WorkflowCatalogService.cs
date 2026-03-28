using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WpfApplication1.Enums;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public class WorkflowCatalogService : IWorkflowCatalogService
    {
        private readonly IWorkflowFileService _workflowFileService;

        public WorkflowCatalogService()
            : this(new WorkflowFileService())
        {
        }

        public WorkflowCatalogService(IWorkflowFileService workflowFileService)
        {
            _workflowFileService = workflowFileService ?? new WorkflowFileService();
            WorkflowRootDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Workflows");
            CatalogDirectory = Path.Combine(WorkflowRootDirectory, "Catalog");
            Directory.CreateDirectory(WorkflowRootDirectory);
            Directory.CreateDirectory(CatalogDirectory);
        }

        public string WorkflowRootDirectory { get; private set; }

        public string CatalogDirectory { get; private set; }

        public async Task<IList<WorkflowListItem>> GetWorkflowsAsync()
        {
            Directory.CreateDirectory(CatalogDirectory);
            var files = Directory.GetFiles(CatalogDirectory, "*.ierpa.json", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var result = new List<WorkflowListItem>();
            foreach (var file in files)
            {
                var workflow = await _workflowFileService.LoadAsync(file);
                if (workflow == null)
                {
                    continue;
                }

                result.Add(new WorkflowListItem
                {
                    WorkflowId = workflow.Id,
                    Name = workflow.Name,
                    WorkflowType = workflow.WorkflowType,
                    Description = workflow.Description,
                    ApplicableRole = workflow.ApplicableRole,
                    SourcePath = file,
                    Version = workflow.Version,
                    LastModifiedAt = workflow.LastModifiedAt ?? File.GetLastWriteTime(file)
                });
            }

            return result;
        }

        public async Task<string> SaveWorkflowAsync(string path, WorkflowDefinition workflow)
        {
            if (workflow == null)
            {
                throw new ArgumentNullException("workflow");
            }

            var targetPath = string.IsNullOrWhiteSpace(path)
                ? BuildUniquePath(workflow.Name, workflow.WorkflowType)
                : path;
            var directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            workflow.LastModifiedAt = DateTime.Now;
            workflow.EnsureCanvasLayout();
            await _workflowFileService.SaveAsync(targetPath, workflow);
            return targetPath;
        }

        public Task<string> CreateWorkflowAsync(WorkflowCreateRequest request, WorkflowDefinition workflow)
        {
            return SaveWorkflowAsync(BuildUniquePath(request != null ? request.Name : null, request != null ? request.WorkflowType : WorkflowType.General), workflow);
        }

        public async Task<string> ImportWorkflowAsync(string importPath)
        {
            if (string.IsNullOrWhiteSpace(importPath) || !File.Exists(importPath))
            {
                throw new FileNotFoundException("Import workflow file was not found.", importPath);
            }

            var workflow = await _workflowFileService.LoadAsync(importPath);
            if (workflow == null)
            {
                throw new InvalidOperationException("Failed to load workflow from import file.");
            }

            if (string.IsNullOrWhiteSpace(workflow.Id))
            {
                workflow.Id = Guid.NewGuid().ToString("N");
            }

            workflow.LastModifiedAt = DateTime.Now;
            workflow.EnsureCanvasLayout();
            var targetPath = BuildUniquePath(workflow.Name, workflow.WorkflowType);
            await _workflowFileService.SaveAsync(targetPath, workflow);
            return targetPath;
        }

        public Task ExportWorkflowAsync(string sourcePath, string exportPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Source workflow file was not found.", sourcePath);
            }

            var directory = Path.GetDirectoryName(exportPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(sourcePath, exportPath, true);
            return Task.FromResult(0);
        }

        public async Task<string> DuplicateWorkflowAsync(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Workflow file was not found.", sourcePath);
            }

            var workflow = await _workflowFileService.LoadAsync(sourcePath);
            if (workflow == null)
            {
                throw new InvalidOperationException("Failed to duplicate workflow.");
            }

            workflow.Id = Guid.NewGuid().ToString("N");
            workflow.Name = (workflow.Name ?? "流程") + " - 副本";
            workflow.IsPublished = false;
            workflow.LastModifiedAt = DateTime.Now;
            workflow.EnsureCanvasLayout();
            var targetPath = BuildUniquePath(workflow.Name, workflow.WorkflowType);
            await _workflowFileService.SaveAsync(targetPath, workflow);
            return targetPath;
        }

        public Task DeleteWorkflowAsync(string sourcePath)
        {
            if (!string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath))
            {
                File.Delete(sourcePath);
            }

            return Task.FromResult(0);
        }

        private string BuildUniquePath(string workflowName, WorkflowType workflowType)
        {
            var typeDirectory = Path.Combine(CatalogDirectory, workflowType.ToString());
            Directory.CreateDirectory(typeDirectory);
            var baseName = SanitizeFileName(string.IsNullOrWhiteSpace(workflowName) ? "workflow" : workflowName);
            var candidate = Path.Combine(typeDirectory, baseName + ".ierpa.json");
            var index = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(typeDirectory, string.Format("{0}_{1}.ierpa.json", baseName, index));
                index++;
            }

            return candidate;
        }

        private static string SanitizeFileName(string raw)
        {
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                raw = raw.Replace(invalidChar, '_');
            }

            return raw;
        }
    }
}
