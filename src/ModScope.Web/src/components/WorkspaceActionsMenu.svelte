<script lang="ts">
  import { onMount } from 'svelte';
  import type { LayoutUiState } from '../contracts';

  import type { ContextMode, ModListMode } from './ui-types';

  export let layout: LayoutUiState;
  export let historyCount = 0;
  export let disabled = false;
  export let onOpenHistory: () => void;
  export let onToggleModList: () => void;
  export let onToggleContext: () => void;
  export let onSetContextMode: (mode: ContextMode) => void;
  export let onSetModListMode: (mode: ModListMode) => void;

  let open = false;
  let menuElement: HTMLDivElement;

  onMount(() => {
    const handleKeydown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && open) {
        event.preventDefault();
        open = false;
      }
    };
    const handlePointerdown = (event: PointerEvent) => {
      if (open && menuElement && !menuElement.contains(event.target as Node)) {
        open = false;
      }
    };

    document.addEventListener('keydown', handleKeydown);
    document.addEventListener('pointerdown', handlePointerdown);
    return () => {
      document.removeEventListener('keydown', handleKeydown);
      document.removeEventListener('pointerdown', handlePointerdown);
    };
  });

  function chooseContextMode(mode: ContextMode) {
    onSetContextMode(mode);
    open = false;
  }

  function chooseModListMode(mode: ModListMode) {
    onSetModListMode(mode);
    open = false;
  }
</script>

<div class="workspace-actions-menu" bind:this={menuElement}>
  <button
    class="more-button"
    class:active={open}
    type="button"
    aria-haspopup="menu"
    aria-expanded={open}
    aria-label="Open More actions"
    disabled={disabled}
    onclick={() => (open = !open)}
  >More <span aria-hidden="true">⌄</span></button>

  {#if open}
    <div class="workspace-actions-menu-popover" role="menu" aria-label="More workspace actions">
      <div class="workspace-actions-menu-group">
        <span class="workspace-actions-menu-label">Browser</span>
        <button type="button" role="menuitem" onclick={() => { onOpenHistory(); open = false; }}>
          History <span>{historyCount}</span>
        </button>
      </div>

      <div class="workspace-actions-menu-group">
        <span class="workspace-actions-menu-label">Context mode</span>
        <button class:active={layout.contextMode === 'context'} type="button" role="menuitem" onclick={() => chooseContextMode('context')}>Context</button>
        <button class:active={layout.contextMode === 'settings'} type="button" role="menuitem" onclick={() => chooseContextMode('settings')}>Settings</button>
        <button class:active={layout.contextMode === 'debug'} type="button" role="menuitem" onclick={() => chooseContextMode('debug')}>Debug</button>
        <button class:active={layout.contextMode === 'analysis'} type="button" role="menuitem" onclick={() => chooseContextMode('analysis')}>Analysis</button>
      </div>

      <div class="workspace-actions-menu-group">
        <span class="workspace-actions-menu-label">Mod Library</span>
        <button class:active={layout.modListMode === 'browse'} type="button" role="menuitem" onclick={() => chooseModListMode('browse')}>Browse</button>
        <button class:active={layout.modListMode === 'deployment-edit'} type="button" role="menuitem" onclick={() => chooseModListMode('deployment-edit')}>Edit profile</button>
      </div>

      <div class="workspace-actions-menu-divider" aria-hidden="true"></div>
      <button type="button" role="menuitem" onclick={() => { onToggleModList(); open = false; }}>
        {layout.modListVisible ? 'Hide' : 'Show'} Mod Library
      </button>
      <button type="button" role="menuitem" onclick={() => { onToggleContext(); open = false; }}>
        {layout.contextVisible ? 'Hide' : 'Show'} Context
      </button>
    </div>
  {/if}
</div>
