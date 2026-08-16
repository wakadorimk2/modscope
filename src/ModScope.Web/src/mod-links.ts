import type { ModCandidateUiState } from './contracts';

export type ModWebsiteLink = {
  url: string | null;
  kind: 'nexus' | 'website' | 'nexus-search' | 'none';
  status: 'Exact' | 'Source' | 'Inferred' | 'No usable URL';
  nexusSearchNames?: string[];
};

function normalizedState(value: string | null | undefined): string {
  return value?.trim().toLowerCase().replace(/[^a-z]+/g, '-') ?? '';
}

function positiveId(value: string | null | undefined): string | null {
  const digits = value?.trim() ?? '';
  if (!/^\d+$/.test(digits)) return null;

  const normalized = digits.replace(/^0+/, '');
  return normalized.length > 0 ? normalized : null;
}

function distinctNames(candidate: ModCandidateUiState): string[] {
  return Array.from(new Set(
    [candidate.displayName, candidate.directoryName, candidate.modKey]
      .map((value) => value?.trim() ?? '')
      .filter((value) => value.length > 0)
  ));
}

function exactNexusModId(candidate: ModCandidateUiState): string | null {
  const relation = candidate.packageRelation;
  if (!relation || normalizedState(relation.identityState) !== 'exact') return null;

  const ids = [
    relation.packageModId,
    ...relation.sourceArtifacts
      .filter((artifact) => normalizedState(artifact.kind) === 'nexus-file')
      .map((artifact) => artifact.modId)
  ]
    .map(positiveId)
    .filter((value): value is string => value !== null);
  const uniqueIds = Array.from(new Set(ids));

  return uniqueIds.length === 1 ? uniqueIds[0] : null;
}

function isWebsiteUrl(value: string | null | undefined): value is string {
  const trimmed = value?.trim();
  if (!trimmed) return false;

  try {
    const url = new URL(trimmed);
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}

function nexusModUrlFromWebsite(value: string): string | null {
  try {
    const url = new URL(value);
    if (url.protocol !== 'https:' || url.hostname.toLowerCase() !== 'www.nexusmods.com') return null;

    const segments = url.pathname.split('/').filter(Boolean);
    const id = segments.length === 3 && segments[0].toLowerCase() === '7daystodie' && segments[1].toLowerCase() === 'mods'
      ? positiveId(segments[2])
      : segments.length === 4 && segments[0].toLowerCase() === 'games' && segments[1].toLowerCase() === '7daystodie' && segments[2].toLowerCase() === 'mods'
        ? positiveId(segments[3])
        : null;

    return id ? `https://www.nexusmods.com/7daystodie/mods/${id}` : null;
  } catch {
    return null;
  }
}

export function resolveModWebsite(candidate: ModCandidateUiState): ModWebsiteLink {
  const modId = exactNexusModId(candidate);
  if (modId) {
    return {
      url: `https://www.nexusmods.com/7daystodie/mods/${modId}`,
      kind: 'nexus',
      status: 'Exact'
    };
  }

  if (isWebsiteUrl(candidate.website)) {
    const nexusWebsiteUrl = nexusModUrlFromWebsite(candidate.website);
    return {
      url: nexusWebsiteUrl ?? candidate.website.trim(),
      kind: nexusWebsiteUrl ? 'nexus' : 'website',
      status: 'Source'
    };
  }

  const names = distinctNames(candidate);
  if (names.length === 0) {
    return { url: null, kind: 'none', status: 'No usable URL' };
  }

  return {
    url: `https://www.nexusmods.com/games/7daystodie/mods?keyword=${encodeURIComponent(names[0])}`,
    kind: 'nexus-search',
    status: 'Inferred',
    nexusSearchNames: names
  };
}
