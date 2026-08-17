<script lang="ts">
  import { onMount } from 'svelte';
  import type { LayoutUiState, WorkspaceActionAvailabilityUiState } from '../contracts';

  import type { ContextMode, ModListMode } from './ui-types';

  export let layout: LayoutUiState;
  export let historyCount = 0;
  export let disabled = false;
  export let showHtmlFallback = false;
  export let onOpenHistory: () => void;
  export let onToggleModList: () => void;
  export let onToggleContext: () => void;
  export let onSetContextMode: (mode: ContextMode) => void;
  export let onSetModListMode: (mode: ModListMode) => void;
  export let onSetMoreOpen: (open: boolean) => void;

  let fallbackOpen = false;
  let menuElement: HTMLDivElement;

  $: if (disabled) fallbackOpen = false;
  $: menuOpen = disabled ? false : (showHtmlFallback ? fallbackOpen : layout.moreOpen);
  $: if (showHtmlFallback && !layout.moreOpen && fallbackOpen) fallbackOpen = false;

  onMount(() => {
    const handleKeydown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && menuOpen) {
        event.preventDefault();
        setMenuOpen(false);
      }
    };
    const handlePointerdown = (event: PointerEvent) => {
      if (showHtmlFallback && menuOpen && menuElement && !menuElement.contains(event.target as Node)) {
        setMenuOpen(false);
      }
    };

    document.addEventListener('keydown', handleKeydown);
    document.addEventListener('pointerdown', handlePointerdown);
    return () => {
      document.removeEventListener('keydown', handleKeydown);
      document.removeEventListener('pointerdown', handlePointerdown);
    };
  });

  function setMenuOpen(nextOpen: boolean) {
    if (disabled) return;
    fallbackOpen = nextOpen;
    onSetMoreOpen(nextOpen);
  }

  function chooseContextMode(mode: ContextMode) {
    if (disabled) return;
    onSetContextMode(mode);
    setMenuOpen(false);
  }

  function chooseModListMode(mode: ModListMode) {
    if (disabled) return;
    onSetModListMode(mode);
    setMenuOpen(false);
  }

  function isActionDisabled(action: WorkspaceActionAvailabilityUiState | null | undefined): boolean {
    return disabled || action?.isEnabled === false;
  }

  function actionTitle(action: WorkspaceActionAvailabilityUiState | null | undefined): string | undefined {
    return action?.isEnabled === false ? `Unavailable: ${action.disabledReason || 'This action is not available.'}` : undefined;
  }
</script>

<div class="workspace-actions-menu" bind:this={menuElement}>
  <button
    class="more-button"
    class:active={menuOpen}
    type="button"
    aria-haspopup="menu"
    aria-expanded={menuOpen}
    aria-label="Open More actions"
    disabled={disabled}
    onclick={() => setMenuOpen(!menuOpen)}
  >More <span aria-hidden="true">⌄</span></button>

  {#if showHtmlFallback && menuOpen}
    <div class="workspace-actions-menu-popover" role="menu" aria-label="More workspace actions">
      <div class="workspace-actions-menu-group">
        <span class="workspace-actions-menu-label">Browser</span>
        <button type="button" role="menuitem" disabled={isActionDisabled(layout.actions?.history)} title={actionTitle(layout.actions?.history)} onclick={() => { onOpenHistory(); setMenuOpen(false); }}>
          <span>History</span><span>{historyCount}</span>
        </button>
      </div>

      <div class="workspace-actions-menu-group">
        <span class="workspace-actions-menu-label">Context mode</span>
        <button class:active={layout.contextMode === 'context'} type="button" role="menuitem" disabled={isActionDisabled(layout.actions?.contextMode)} title={actionTitle(layout.actions?.contextMode)} onclick={() => chooseContextMode('context')}>Context</button>
        <button class:active={layout.contextMode === 'settings'} type="button" role="menuitem" disabled={isActionDisabled(layout.actions?.settingsMode)} title={actionTitle(layout.actions?.settingsMode)} onclick={() => chooseContextMode('settings')}>Settings</button>
        <button class:active={layout.contextMode === 'debug'} type="button" role="menuitem" disabled={isActionDisabled(layout.actions?.debugMode)} title={actionTitle(layout.actions?.debugMode)} onclick={() => chooseContextMode('debug')}>Debug</button>
        <button class:active={layout.contextMode === 'analysis'} type="button" role="menuitem" disabled={isActionDisabled(layout.actions?.analysisMode)} title={actionTitle(layout.actions?.analysisMode)} onclick={() => chooseContextMode('analysis')}>Analysis{#if layout.actions?.analysisMode?.isEnabled === false}<span class="workspace-actions-menu-reason">{layout.actions.analysisMode.disabledReason}</span>{/if}</button>
      </div>

      <div class="workspace-actions-menu-group">
        <span class="workspace-actions-menu-label">Mod Library</span>
        <button class:active={layout.modListMode === 'browse'} type="button" role="menuitem" disabled={isActionDisabled(layout.actions?.browseModList)} title={actionTitle(layout.actions?.browseModList)} onclick={() => chooseModListMode('browse')}>Browse</button>
        <button class:active={layout.modListMode === 'deployment-edit'} type="button" role="menuitem" disabled={isActionDisabled(layout.actions?.editProfile)} title={actionTitle(layout.actions?.editProfile)} onclick={() => chooseModListMode('deployment-edit')}>Edit profile{#if layout.actions?.editProfile?.isEnabled === false}<span class="workspace-actions-menu-reason">{layout.actions.editProfile.disabledReason}</span>{/if}</button>
      </div>

      <div class="workspace-actions-menu-divider" aria-hidden="true"></div>
      <button type="button" role="menuitem" disabled={isActionDisabled(layout.actions?.toggleModList)} title={actionTitle(layout.actions?.toggleModList)} onclick={() => { onToggleModList(); setMenuOpen(false); }}>
        {layout.modListVisible ? 'Hide' : 'Show'} Mod Library
      </button>
      <button type="button" role="menuitem" disabled={isActionDisabled(layout.actions?.toggleContext)} title={actionTitle(layout.actions?.toggleContext)} onclick={() => { onToggleContext(); setMenuOpen(false); }}>
        {layout.contextVisible ? 'Hide' : 'Show'} Context
      </button>
    </div>
  {/if}
</div>

<style>
  .workspace-actions-menu {
    position: relative;
    flex: 0 0 auto;
  }

  .more-button {
    display: inline-flex;
    min-height: 30px;
    align-items: center;
    gap: 5px;
    padding: 6px 9px;
    border: 1px solid transparent;
    border-radius: 5px;
    background: transparent;
    color: #9aa0a6;
    font-size: 11px;
    font-weight: 400;
  }

  .more-button:hover:not(:disabled),
  .more-button.active {
    border-color: transparent;
    background: #3a3b3f;
    color: #e8eaed;
  }

  .workspace-actions-menu-popover {
    position: absolute;
    top: calc(100% + 8px);
    right: 0;
    z-index: 20;
    display: grid;
    width: 230px;
    gap: 4px;
    padding: 8px;
    border: 1px solid #3c4043;
    border-radius: 7px;
    background: #202124;
    box-shadow: 0 18px 40px rgba(0, 0, 0, 0.42);
  }

  .workspace-actions-menu-group {
    display: grid;
    gap: 2px;
  }

  .workspace-actions-menu-label {
    padding: 5px 8px 3px;
    color: #8ab4f8;
    font-size: 10px;
    font-weight: 700;
    letter-spacing: 0.06em;
    text-transform: uppercase;
  }

  .workspace-actions-menu-popover button {
    display: flex;
    width: 100%;
    min-height: 28px;
    align-items: center;
    justify-content: space-between;
    padding: 5px 8px;
    border: 0;
    border-radius: 5px;
    background: transparent;
    color: #bdc1c6;
    font-size: 11px;
    text-align: left;
  }

  .workspace-actions-menu-reason {
    max-width: 125px;
    margin-left: 8px;
    color: #9aa0a6;
    font-size: 9px;
    line-height: 1.25;
    text-align: right;
  }

  .workspace-actions-menu-popover button:hover:not(:disabled),
  .workspace-actions-menu-popover button.active {
    background: #35445b;
    color: #e8eaed;
  }

  .workspace-actions-menu-divider {
    height: 1px;
    margin: 4px 0;
    background: #3c4043;
  }

  .workspace-actions-menu button:focus-visible {
    outline: 2px solid #8ab4f8;
    outline-offset: 1px;
  }
</style>
