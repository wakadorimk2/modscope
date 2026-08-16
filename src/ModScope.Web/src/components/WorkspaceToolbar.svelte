<script lang="ts">
  import WorkspaceActionsMenu from './WorkspaceActionsMenu.svelte';
  import type { ContextMode, ModListMode } from './ui-types';
  import type { BridgeErrorPayload, LayoutUiState, UiState } from '../contracts';

  export let state: UiState;
  export let layout: LayoutUiState;
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

<main class="toolbar-surface mock-toolbar-surface">
  <div class="toolbar-tabs-row toolbar-row mock-toolbar-tabs-row">
    <div class="browser-tabs" role="tablist" aria-label="Browser tabs">
      {#each state.browser.tabs as tab (tab.tabId)}
        <div class:active={tab.isActive} class="browser-tab">
          {#if tab.isActive}<span class="browser-tab-dot" aria-hidden="true"></span>{/if}
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

  <div class="toolbar-controls-row toolbar-row mock-toolbar-controls-row">
    <div class="toolbar-navigation" aria-label="Browser navigation">
      <button class="icon-button" title="Back" aria-label="Back" disabled={disabled || !state.browser.canGoBack} onclick={onBack}>‹</button>
      <button class="icon-button" title="Forward" aria-label="Forward" disabled={disabled || !state.browser.canGoForward} onclick={onForward}>›</button>
      <button class="icon-button" title="Reload" aria-label="Reload" disabled={disabled} onclick={onReload}>↻</button>
      <button class="icon-button toolbar-home-button" title="Home" aria-label="Open Browse Home" disabled={disabled} onclick={onHome}>⌂</button>
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
      <button class="secondary-button toolbar-go-button" type="button" title="Go" aria-label="Go" disabled={disabled} onclick={onNavigate}>Go</button>
    </div>

    <div class="toolbar-actions" aria-label="Workspace actions">
      <button
        class="history-button toolbar-text-action"
        type="button"
        title={`Open history (${state.browser.history.length} entries)`}
        aria-label={`Open history (${state.browser.history.length} entries)`}
        disabled={disabled}
        onclick={onOpenHistory}
      >History</button>
      <button
        class="pane-toggle-button toolbar-text-action"
        class:active={layout.modListVisible}
        type="button"
        title={layout.modListVisible ? 'Hide Mod Library pane' : 'Show Mod Library pane'}
        aria-label={layout.modListVisible ? 'Hide Mod Library pane' : 'Show Mod Library pane'}
        aria-pressed={layout.modListVisible}
        disabled={disabled}
        onclick={onToggleModList}
      >Library</button>
      <button
        class="pane-toggle-button toolbar-text-action"
        class:active={layout.contextVisible}
        type="button"
        title={layout.contextVisible ? 'Hide Context pane' : 'Show Context pane'}
        aria-label={layout.contextVisible ? 'Hide Context pane' : 'Show Context pane'}
        aria-pressed={layout.contextVisible}
        disabled={disabled}
        onclick={onToggleContext}
      >Context</button>
      <WorkspaceActionsMenu
        {layout}
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

<style>
  .mock-toolbar-surface {
    height: 86px;
    min-height: 86px;
    background: #292a2d;
    color: #e8eaed;
  }

  .mock-toolbar-tabs-row {
    flex: 0 0 38px;
    align-items: flex-end;
    min-height: 38px;
    gap: 10px;
    padding: 4px 14px 0;
    border-bottom: 1px solid #202124;
    background: #292a2d;
    overflow: hidden;
  }

  .mock-toolbar-tabs-row .browser-tabs {
    align-items: flex-end;
    gap: 10px;
    padding-top: 0;
    overflow-x: auto;
  }

  .mock-toolbar-tabs-row .browser-tab {
    min-width: 210px;
    max-width: 310px;
    height: 29px;
    padding: 0 0 0 11px;
    border: 0;
    border-radius: 7px 7px 0 0;
    background: transparent;
    color: #9aa0a6;
    font-size: 12px;
  }

  .mock-toolbar-tabs-row .browser-tab.active {
    background: #303134;
    color: #e8eaed;
  }

  .browser-tab-dot {
    width: 7px;
    height: 7px;
    flex: 0 0 7px;
    border-radius: 50%;
    background: #8ab4f8;
  }

  .mock-toolbar-tabs-row .browser-tab-select {
    height: 100%;
    padding: 0;
  }

  .mock-toolbar-tabs-row .browser-tab-close {
    margin-right: 4px;
    color: #6f747a;
  }

  .mock-toolbar-tabs-row .browser-tab-close:hover:not(:disabled) {
    color: #e8eaed;
    background: #3a3b3f;
  }

  .mock-toolbar-tabs-row .browser-new-tab {
    align-self: center;
    margin: 0 0 1px 0;
    color: #9aa0a6;
  }

  .mock-toolbar-tabs-row .browser-new-tab:hover:not(:disabled) {
    background: #3a3b3f;
    color: #e8eaed;
  }

  .mock-toolbar-controls-row {
    flex: 0 0 48px;
    min-height: 48px;
    height: 48px;
    gap: 10px;
    padding: 8px 14px;
    background: #292a2d;
    overflow: visible;
  }

  .mock-toolbar-controls-row .toolbar-navigation {
    gap: 0;
  }

  .mock-toolbar-controls-row .toolbar-navigation .icon-button {
    width: 27px;
    height: 27px;
    flex: 0 0 27px;
    border: 0;
    border-radius: 5px;
    background: transparent;
    color: #9aa0a6;
    font-size: 16px;
  }

  .mock-toolbar-controls-row .toolbar-navigation .icon-button:hover:not(:disabled) {
    background: #3a3b3f;
    color: #e8eaed;
  }

  .mock-toolbar-controls-row .toolbar-omnibox {
    flex: 1 1 auto;
    height: 30px;
    padding: 0 5px 0 12px;
    border: 1px solid #3c4043;
    border-radius: 15px;
    background: #202124;
  }

  .mock-toolbar-controls-row .toolbar-address {
    height: 30px;
    color: #e8eaed;
    font-size: 12px;
  }

  .mock-toolbar-controls-row .toolbar-go-button {
    display: inline-flex;
    width: auto;
    height: 28px;
    min-height: 28px;
    align-items: center;
    justify-content: center;
    padding: 0 9px;
    border: 1px solid transparent;
    border-radius: 5px;
    background: transparent;
    color: #9aa0a6;
    font-size: 11px;
    font-weight: 600;
  }

  .mock-toolbar-controls-row .toolbar-go-button:hover:not(:disabled) {
    background: #3a3b3f;
    color: #e8eaed;
  }

  .mock-toolbar-controls-row .toolbar-actions {
    gap: 2px;
    overflow: visible;
  }

  .mock-toolbar-controls-row .toolbar-text-action {
    display: inline-flex;
    width: auto;
    height: 30px;
    min-height: 30px;
    flex: 0 0 auto;
    align-items: center;
    justify-content: center;
    padding: 6px 9px;
    border: 1px solid transparent;
    border-radius: 5px;
    background: transparent;
    color: #9aa0a6;
    font-size: 11px;
    font-weight: 400;
  }

  .mock-toolbar-controls-row .toolbar-text-action:hover:not(:disabled),
  .mock-toolbar-controls-row .toolbar-text-action.active {
    background: #3a3b3f;
    color: #e8eaed;
  }

  .mock-toolbar-controls-row .toolbar-text-action.active {
    background: rgba(138, 180, 248, 0.18);
    color: #8ab4f8;
  }

  .mock-toolbar-controls-row .pane-toggle-button {
    color: #858b93;
  }

  .mock-toolbar-controls-row .pane-toggle-button.active {
    background: rgba(138, 180, 248, 0.1);
    color: #a9c8f2;
  }

  .mock-toolbar-controls-row .shortcut-hint {
    color: #6f747a;
  }

  .mock-toolbar-surface button:focus-visible,
  .mock-toolbar-surface input:focus-visible {
    outline: 2px solid #8ab4f8;
    outline-offset: 1px;
  }

  @media (max-width: 900px) {
    .mock-toolbar-controls-row {
      gap: 5px;
      padding-right: 8px;
      padding-left: 8px;
    }

    .mock-toolbar-controls-row .toolbar-text-action {
      padding-right: 6px;
      padding-left: 6px;
    }
  }
</style>
