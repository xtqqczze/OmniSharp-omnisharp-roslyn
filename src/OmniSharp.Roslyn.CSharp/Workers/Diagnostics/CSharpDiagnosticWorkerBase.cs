using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.FileSystemGlobbing;
using OmniSharp.FileSystem;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;

namespace OmniSharp.Roslyn.CSharp.Workers.Diagnostics;

public abstract class CSharpDiagnosticWorkerBase : ICsDiagnosticWorker
{
    private readonly OmniSharpWorkspace _workspace;
    private readonly FileSystemHelper _fileSystemHelper;
    private readonly Matcher _fileSystemMatcher;

    public CSharpDiagnosticWorkerBase(
            OmniSharpWorkspace workspace,
            FileSystemHelper fileSystemHelper)
    {
        _workspace = workspace;
        _fileSystemHelper = fileSystemHelper;
        _fileSystemMatcher = _fileSystemHelper.BuildExcludeMatcher();
    }

    public abstract bool AnalyzersEnabled { get; }

    public abstract Task<IEnumerable<Diagnostic>> AnalyzeDocumentAsync(Document document, CancellationToken cancellationToken);

    public abstract Task<IEnumerable<Diagnostic>> AnalyzeProjectsAsync(Project project, CancellationToken cancellationToken);

    public abstract Task<ImmutableArray<DocumentDiagnostics>> GetAllDiagnosticsAsync();

    public abstract Task<ImmutableArray<DocumentDiagnostics>> GetDiagnostics(ImmutableArray<string> documentPaths);

    public abstract ImmutableArray<DocumentId> QueueDocumentsForDiagnostics(IEnumerable<Document> documents, AnalyzerWorkType workType);

    public ImmutableArray<DocumentId> QueueDocumentsForDiagnostics()
    {
        IEnumerable<Document> documents = _workspace.CurrentSolution.Projects.SelectMany(x => x.Documents);
        documents = FilterDocuments(documents);
        return QueueDocumentsForDiagnostics(documents, AnalyzerWorkType.Background);
    }

    public ImmutableArray<DocumentId> QueueDocumentsForDiagnostics(ImmutableArray<ProjectId> projectId)
    {
        IEnumerable<Document> documents = projectId.SelectMany(projectId => _workspace.CurrentSolution.GetProject(projectId).Documents);
        documents = FilterDocuments(documents);
        return QueueDocumentsForDiagnostics(documents, AnalyzerWorkType.Background);
    }

    protected ImmutableArray<DocumentId> QueueDocumentsForDiagnostics(Project project)
    {
        IEnumerable<Document> documents = project.Documents;
        documents = FilterDocuments(documents);
        return QueueDocumentsForDiagnostics(documents, AnalyzerWorkType.Background);
    }

    protected ImmutableArray<DocumentId> QueueDocumentsForDiagnostics(IEnumerable<DocumentId> documentsIds, AnalyzerWorkType workType)
    {
        IEnumerable<Document> documents = documentsIds.Select(x => _workspace.CurrentSolution.GetDocument(x));
        documents = FilterDocuments(documents);
        return QueueDocumentsForDiagnostics(documents, workType);
    }

    private ImmutableArray<Document> FilterDocuments(IEnumerable<Document> documents) =>
        documents
            .Where(x => !FileSystemHelper.AutoGenerated(x.FilePath))
            .Where(x => !_fileSystemMatcher
                    .Match(FileSystemHelper
                        .GetRelativePath(x.FilePath, _fileSystemHelper.TargetDirectory)
                        ?? x.FilePath)
                    .HasMatches)
            .ToImmutableArray();
}