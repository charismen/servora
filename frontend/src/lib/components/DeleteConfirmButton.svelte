<script lang="ts">
	let {
		label,
		confirmTitle,
		confirmMessage,
		cancelLabel,
		confirmLabel = label,
		confirmIcon = 'delete',
		icon = undefined,
		iconOnly = false,
		title = undefined,
		ariaLabel = undefined,
		disabled = false,
		variant = 'danger',
		submitting = false,
		formaction,
		class: className = ''
	}: {
		label: string;
		confirmTitle: string;
		confirmMessage: string;
		cancelLabel: string;
		confirmLabel?: string;
		confirmIcon?: string;
		icon?: string;
		iconOnly?: boolean;
		title?: string;
		ariaLabel?: string;
		disabled?: boolean;
		variant?: 'danger' | 'warning';
		submitting?: boolean;
		formaction?: string;
		class?: string;
	} = $props();

	let dialog: HTMLDialogElement | undefined;
	let wasSubmitting = false;

	$effect(() => {
		if (wasSubmitting && !submitting && dialog?.open) {
			dialog.close();
		}
		wasSubmitting = submitting;
	});

	export function close() {
		if (dialog?.open) {
			dialog.close();
		}
	}

	function handleDialogClick(event: MouseEvent) {
		if (!dialog || submitting) return;
		const rect = dialog.getBoundingClientRect();
		const isInDialog =
			rect.top <= event.clientY &&
			event.clientY <= rect.top + rect.height &&
			rect.left <= event.clientX &&
			event.clientX <= rect.left + rect.width;
		if (!isInDialog) {
			dialog.close();
		}
	}

	function handleDialogCancel(event: Event) {
		if (submitting) {
			event.preventDefault();
		}
	}

	const badgeClasses = $derived(
		variant === 'warning'
			? 'bg-amber-50 border-amber-200 text-amber-700'
			: 'bg-rose-50 border-rose-200 text-rose-600'
	);

	const confirmButtonClasses = $derived(
		variant === 'warning'
			? 'border-warning/30 bg-warning hover:bg-warning/90'
			: 'border-error/30 bg-error hover:bg-error/90'
	);
</script>

<button
	class={className}
	type="button"
	disabled={disabled || submitting}
	{title}
	aria-label={ariaLabel ?? (iconOnly ? label : undefined)}
	onclick={() => dialog?.showModal()}
>
	{#if icon}
		<span class="material-symbols-outlined text-[15px]" aria-hidden="true">{icon}</span>
	{/if}
	{#if !iconOnly}
		<span>{label}</span>
	{/if}
</button>

<!-- svelte-ignore a11y_click_events_have_key_events -->
<!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
<dialog bind:this={dialog} class="delete-dialog" onclick={handleDialogClick} oncancel={handleDialogCancel}>
	<div class="space-y-4">
		<div class="flex items-start gap-3.5">
			<div class="flex h-10 w-10 shrink-0 items-center justify-center rounded-full border shadow-2xs mt-0.5 {badgeClasses}">
				<span class="material-symbols-outlined text-[20px]" aria-hidden="true">warning</span>
			</div>
			<div class="min-w-0 flex-1">
				<h3 class="font-subheading text-subheading font-semibold text-primary leading-snug m-0">{confirmTitle}</h3>
				<p class="mt-1 text-body-sm text-secondary leading-relaxed m-0">{confirmMessage}</p>
			</div>
		</div>
		<div class="flex justify-end gap-2.5 pt-2 border-t border-stone/60">
			<button
				class="rounded-button border border-stone bg-white px-4 py-2 text-body-sm font-medium text-secondary hover:text-primary hover:bg-ash active:scale-[0.98] transition-all shadow-2xs cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed disabled:active:scale-100"
				type="button"
				disabled={submitting}
				onclick={() => dialog?.close()}
			>
				{cancelLabel}
			</button>
			<button
				class="rounded-button border px-4 py-2 text-body-sm font-semibold text-white active:scale-[0.98] transition-all shadow-xs cursor-pointer inline-flex items-center gap-1.5 disabled:opacity-60 disabled:cursor-not-allowed disabled:active:scale-100 {confirmButtonClasses}"
				type="submit"
				disabled={submitting}
				{formaction}
			>
				{#if submitting}
					<span class="material-symbols-outlined text-[16px] animate-spin" aria-hidden="true">progress_activity</span>
				{:else}
					<span class="material-symbols-outlined text-[16px]" aria-hidden="true">{confirmIcon}</span>
				{/if}
				<span>{confirmLabel}</span>
			</button>
		</div>
	</div>
</dialog>

<style>
	.delete-dialog {
		position: fixed;
		inset: 0;
		margin: auto;
		width: min(440px, calc(100vw - 32px));
		height: fit-content;
		max-height: calc(100vh - 32px);
		border: 1px solid var(--color-stone, #e6dfd5);
		border-radius: 12px;
		padding: 20px 24px;
		background: #ffffff;
		box-shadow: 0 20px 25px -5px rgb(0 0 0 / 0.1), 0 8px 10px -6px rgb(0 0 0 / 0.1);
		color: var(--color-ink, #1c1815);
		text-align: left;
		white-space: normal;
		overflow-x: hidden;
		overflow-y: auto;
	}

	.delete-dialog::backdrop {
		background: rgba(0, 0, 0, 0.45);
		backdrop-filter: blur(2px);
	}

	.delete-dialog[open] {
		animation: dialog-appear 0.15s cubic-bezier(0.16, 1, 0.3, 1);
	}

	@keyframes dialog-appear {
		from {
			opacity: 0;
			transform: scale(0.96);
		}
		to {
			opacity: 1;
			transform: scale(1);
		}
	}
</style>
