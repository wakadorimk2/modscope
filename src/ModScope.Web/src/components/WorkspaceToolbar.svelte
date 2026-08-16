<script lang="ts">
  import WorkspaceActionsMenu from './WorkspaceActionsMenu.svelte';
  import type { ContextMode, ModListMode } from './ui-types';
  import type { BridgeErrorPayload, UiState } from '../contracts';

  export let state: UiState;
  export let address = '';
  export let disabled = false;
  export let showHtmlMoreMenu = false;
  export let error: BridgeErrorPayload | null = null;
  export let onNavigate: () => void;
  export let onBack: () => void;
  export let onForward: () => void;
  export let onReload: () => void;
  export let onHome: () => void;
  export let onOpenHistory: () => void;
  export let onNewTab: () => void;
  export let onSelectTab: (tabId: string) => void;
  export let onCloseTab: (tabId: string) => void;
  export let onToggleModList: () => void;
  export let onToggleContext: () => void;
  export let onSetContextMode: (mode: ContextMode) => void;
  export let onSetModListMode: (mode: ModListMode) => void;
  export let onSetMoreOpen: (open: boolean) => void;
</script>

<main class="toolbar-surface">
  <div class="toolbar-tabs-row toolbar-row">
    <div class="browser-tabs" role="tablist" aria-label="Browser tabs">
      {#each state.browser.tabs as tab (tab.tabId)}
        <div class:active={tab.isActive} class="browser-tab">
          <button
            type="button"
            role="tab"
            class="browser-tab-select"
            aria-label={`Select tab ${tab.title || 'New tab'}`}
            aria-selected={tab.isActive}
            disabled={disabled}
            onclick={() => onSelectTab(tab.tabId)}
          ><span>{tab.title || 'New tab'}</span></button>
          <button
            type="button"
            class="browser-tab-close"
            title="Close tab"
            aria-label={`Close tab ${tab.title || 'New tab'}`}
            disabled={disabled}
            onclick={() => onCloseTab(tab.tabId)}
          >×</button>
        </div>
      {/each}
      <button type="button" class="icon-button compact-icon-button browser-new-tab" title="New tab" aria-label="New tab" disabled={disabled} onclick={onNewTab}>+</button>
    </div>
  </div>

  <div class="toolbar-controls-row toolbar-row">
    <div class="toolbar-navigation" aria-label="Browser navigation">
      <button class="icon-button" title="Back" aria-label="Back" disabled={disabled || !state.browser.canGoBack} onclick={onBack}>←</button>
      <button class="icon-button" title="Forward" aria-label="Forward" disabled={disabled || !state.browser.canGoForward} onclick={onForward}>→</button>
      <button class="icon-button" title="Reload" aria-label="Reload" disabled={disabled} onclick={onReload}>↻</button>
      <button class="icon-button" title="Home" aria-label="Open Browse Home" disabled={disabled} onclick={onHome}>⌂</button>
    </div>

    <div class="toolbar-omnibox" aria-label="Browser address controls">
      <input
        class="toolbar-address"
        aria-label="Browser URL"
        placeholder="https://example.com"
        bind:value={address}
        disabled={disabled}
        onkeydown={(event) => event.key === 'Enter' && onNavigate()}
      />
      <button class="secondary-button toolbar-go-button" type="button" title="Go" aria-label="Go" disabled={disabled} onclick={onNavigate}>↵</button>
    </div>

    <div class="toolbar-actions" aria-label="Workspace actions">
      <button
        class="history-button"
        type="button"
        title={`Open history (${state.browser.history.length} entries)`}
        aria-label={`Open history (${state.browser.history.length} entries)`}
        disabled={disabled}
        onclick={onOpenHistory}
      >◷</button>
      <button
        class="pane-toggle-button"
        class:active={state.layout.modListVisible}
        type="button"
        title={state.layout.modListVisible ? 'Hide Mod Library pane' : 'Show Mod Library pane'}
        aria-label={state.layout.modListVisible ? 'Hide Mod Library pane' : 'Show Mod Library pane'}
        aria-pressed={state.layout.modListVisible}
        disabled={disabled}
        onclick={onToggleModList}
      ><span aria-hidden="true">◧</span></button>
      <button
        class="pane-toggle-button"
        class:active={state.layout.contextVisible}
        type="button"
        title={state.layout.contextVisible ? 'Hide Context pane' : 'Show Context pane'}
        aria-label={state.layout.contextVisible ? 'Hide Context pane' : 'Show Context pane'}
        aria-pressed={state.layout.contextVisible}
        disabled={disabled}
        onclick={onToggleContext}
      ><span aria-hidden="true">◨</span></button>
      <WorkspaceActionsMenu
        layout={state.layout}
        historyCount={state.browser.history.length}
        {disabled}
        showHtmlFallback={showHtmlMoreMenu}
        onOpenHistory={onOpenHistory}
        onToggleModList={onToggleModList}
        onToggleContext={onToggleContext}
        onSetContextMode={onSetContextMode}
        onSetModListMode={onSetModListMode}
        onSetMoreOpen={onSetMoreOpen}
      />
      <span class="shortcut-hint">Ctrl/Cmd+I</span>
    </div>
  </div>

  {#if error}
    <p class="error-notice"><strong>{error.code}</strong> {error.message}</p>
  {/if}
</main>
