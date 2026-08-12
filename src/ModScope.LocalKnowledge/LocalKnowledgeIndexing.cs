namespace ModScope.LocalKnowledge;

public static class LocalKnowledgeIndexBuilder
{
    public static LocalKnowledgeIndex Build(IReadOnlyList<LocalModRecord> mods)
    {
        ArgumentNullException.ThrowIfNull(mods);

        var forward = new List<LocalKnowledgeReference>();
        foreach (var mod in mods.OrderBy(mod => mod.ModKey, StringComparer.OrdinalIgnoreCase))
        {
            var modNode = new LocalKnowledgeNode(LocalKnowledgeNodeKind.Mod, mod.ModKey);
            foreach (var file in mod.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal))
            {
                var fileValue = BuildFileValue(
                    mod.ResolvedDirectoryRelativePath ?? mod.DirectoryName,
                    file.RelativePath);
                var fileNode = new LocalKnowledgeNode(LocalKnowledgeNodeKind.File, fileValue);
                AddReference(forward, modNode, fileNode, LocalKnowledgeRelation.Contains, file.Evidence);

                var xml = mod.XmlFiles.FirstOrDefault(xmlFile =>
                    xmlFile.RelativePath.Equals(file.RelativePath, StringComparison.Ordinal));
                if (xml is null)
                {
                    continue;
                }

                var xmlNode = new LocalKnowledgeNode(LocalKnowledgeNodeKind.XmlFile, fileValue);
                AddReference(
                    forward,
                    fileNode,
                    xmlNode,
                    LocalKnowledgeRelation.Contains,
                    new EvidenceReference(EvidenceKind.Normalized, xml.Source));

                foreach (var operation in xml.PatchOperations.OrderBy(
                             operation => operation.ElementPath,
                             StringComparer.Ordinal))
                {
                    var operationNode = new LocalKnowledgeNode(
                        LocalKnowledgeNodeKind.PatchOperation,
                        $"{fileValue}#{operation.ElementPath}");
                    AddReference(
                        forward,
                        xmlNode,
                        operationNode,
                        LocalKnowledgeRelation.Contains,
                        new EvidenceReference(EvidenceKind.Source, operation.Source));

                    AddCandidateReferences(
                        forward,
                        operationNode,
                        operation.TargetXmlCandidates,
                        LocalKnowledgeNodeKind.TargetXml,
                        LocalKnowledgeRelation.Targets);
                    AddXPathReferences(forward, operationNode, operation.XPathCandidates);
                    AddCandidateReferences(
                        forward,
                        operationNode,
                        operation.EntityCandidates,
                        LocalKnowledgeNodeKind.Entity,
                        LocalKnowledgeRelation.Mentions);
                    AddCandidateReferences(
                        forward,
                        operationNode,
                        operation.PropertyCandidates,
                        LocalKnowledgeNodeKind.Property,
                        LocalKnowledgeRelation.Mentions);
                    AddCandidateReferences(
                        forward,
                        operationNode,
                        operation.AttributeCandidates,
                        LocalKnowledgeNodeKind.Attribute,
                        LocalKnowledgeRelation.Mentions);
                }
            }
        }

        var orderedForward = forward
            .OrderBy(ReferenceSortKey, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
        var orderedReverse = orderedForward
            .Select(reference => new LocalKnowledgeReference(
                reference.To,
                reference.From,
                reference.Relation,
                reference.Evidence))
            .OrderBy(ReferenceSortKey, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();

        return new LocalKnowledgeIndex(orderedForward, orderedReverse);
    }

    private static void AddCandidateReferences(
        ICollection<LocalKnowledgeReference> references,
        LocalKnowledgeNode operationNode,
        IEnumerable<XmlReferenceCandidate> candidates,
        LocalKnowledgeNodeKind targetKind,
        LocalKnowledgeRelation relation)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.NormalizedValue is null)
            {
                continue;
            }

            references.Add(new LocalKnowledgeReference(
                operationNode,
                new LocalKnowledgeNode(targetKind, candidate.NormalizedValue),
                relation,
                new EvidenceReference(candidate.EvidenceKind, candidate.Source)));
        }
    }

    private static void AddXPathReferences(
        ICollection<LocalKnowledgeReference> references,
        LocalKnowledgeNode operationNode,
        IEnumerable<XmlXPathCandidate> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.NormalizedValue is null)
            {
                continue;
            }

            references.Add(new LocalKnowledgeReference(
                operationNode,
                new LocalKnowledgeNode(LocalKnowledgeNodeKind.XPath, candidate.NormalizedValue),
                LocalKnowledgeRelation.Selects,
                new EvidenceReference(EvidenceKind.Normalized, candidate.Source)));
        }
    }

    private static void AddReference(
        ICollection<LocalKnowledgeReference> references,
        LocalKnowledgeNode from,
        LocalKnowledgeNode to,
        LocalKnowledgeRelation relation,
        EvidenceReference evidence)
    {
        references.Add(new LocalKnowledgeReference(from, to, relation, evidence));
    }

    private static string BuildFileValue(string directoryRelativePath, string relativePath)
    {
        return ParsingUtilities.BuildSourcePath(
            ParsingUtilities.BuildSourcePath("mods", directoryRelativePath),
            relativePath);
    }

    private static string ReferenceSortKey(LocalKnowledgeReference reference)
    {
        return string.Join(
            '\0',
            (int)reference.From.Kind,
            reference.From.Value,
            (int)reference.Relation,
            (int)reference.To.Kind,
            reference.To.Value,
            (int)reference.Evidence.Kind,
            reference.Evidence.Source.RelativePath,
            reference.Evidence.Source.LineNumber ?? -1,
            reference.Evidence.Source.ColumnNumber ?? -1);
    }
}
