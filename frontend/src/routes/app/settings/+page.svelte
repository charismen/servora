<script lang="ts">
	import { onMount } from 'svelte';
	import { enhance } from '$app/forms';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import Dropdown from '$lib/components/ui/dropdown/Dropdown.svelte';
	import TimePicker from '$lib/components/ui/time-picker/TimePicker.svelte';
	import { createTranslator, locales } from '$lib/i18n';
	import { apiAssetUrl, compressImage, isSupportedImageFile } from '$lib/media';
	import { toast } from '$lib/stores/toast.svelte';
	import { formatEnhanceUnexpectedError } from '$lib/utils/http';
	import { handlePhoneInput, handlePhonePaste } from '$lib/utils/phone';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	let settings = $derived(form?.settings ?? data.settings);
	const logoUrl = $derived(apiAssetUrl(settings.logoUrl));
	let selectedLogoUrl = $state('');
	let selectedLogoName = $state('');
	let settingsForm: HTMLFormElement;
	let showRiskyConfirm = $state(false);
	let showResetConfirm = $state(false);
	let allowRiskySubmit = false;
	let saving = $state(false);
	let resetting = $state(false);
	let isScrolled = $state(false);
	let isMac = $state(true);
	const logoPreviewUrl = $derived(selectedLogoUrl || logoUrl);
	let activeSection = $state('general');

	onMount(() => {
		isMac = typeof navigator !== 'undefined' && /Mac|iPod|iPhone|iPad/.test(navigator.platform || navigator.userAgent);
		function handleScroll(event: Event | null = null) {
			const scrollPos = (event?.target as HTMLElement)?.scrollTop ?? window.scrollY ?? 0;
			isScrolled = scrollPos > 80;
			const sections = ['general', 'numbering', 'operational'];
			const checkPos = scrollPos + 200;
			for (const id of sections) {
				const el = document.getElementById(id);
				if (el) {
					const top = el.offsetTop;
					const height = el.offsetHeight;
					if (checkPos >= top && checkPos < top + height) {
						activeSection = id;
						break;
					}
				}
			}
		}
		window.addEventListener('scroll', handleScroll, { passive: true, capture: true });
		handleScroll();
		return () => window.removeEventListener('scroll', handleScroll, { capture: true });
	});

	function handleKeydown(e: KeyboardEvent) {
		if ((e.metaKey || e.ctrlKey) && e.key === 's') {
			e.preventDefault();
			settingsForm?.requestSubmit();
		}
	}

	function handleLogoChange(e: Event) {
		const input = e.currentTarget as HTMLInputElement;
		const file = input.files?.[0];
		if (selectedLogoUrl) URL.revokeObjectURL(selectedLogoUrl);
		selectedLogoUrl = file ? URL.createObjectURL(file) : '';
		selectedLogoName = file?.name ?? '';
	}

	// Local state for prefix live preview reactive to user input
	function initialSettings() {
		return form?.settings ?? data.settings;
	}

	let ticketPrefix = $state(initialSettings().ticketPrefix || '');
	let ticketDateFormat = $state(initialSettings().ticketDateFormat || 'yyyyMMdd');
	let ticketStartNumber = $state(initialSettings().ticketStartNumber || 1);
	let ticketMinDigits = $state(initialSettings().ticketMinDigits || 4);

	const todayStamp = $derived.by(() => {
		const now = new Date();
		const yyyy = now.getFullYear().toString();
		const yy = yyyy.slice(-2);
		const mm = String(now.getMonth() + 1).padStart(2, '0');
		const dd = String(now.getDate()).padStart(2, '0');
		if (ticketDateFormat === 'yyyyMM') return `${yyyy}${mm}`;
		if (ticketDateFormat === 'yyMMdd') return `${yy}${mm}${dd}`;
		return `${yyyy}${mm}${dd}`;
	});

	// Sync local state when settings loads/changes
	$effect(() => {
		ticketPrefix = settings.ticketPrefix || '';
		ticketDateFormat = settings.ticketDateFormat || 'yyyyMMdd';
		ticketStartNumber = settings.ticketStartNumber || 1;
		ticketMinDigits = settings.ticketMinDigits || 4;
	});

	function confirmRiskyChange(event: SubmitEvent) {
		if (
			ticketPrefix !== settings.ticketPrefix ||
			ticketDateFormat !== settings.ticketDateFormat ||
			ticketStartNumber !== settings.ticketStartNumber ||
			ticketMinDigits !== settings.ticketMinDigits
		) {
			if (!allowRiskySubmit) {
				event.preventDefault();
				showRiskyConfirm = true;
				return;
			}
		}
		allowRiskySubmit = false;
	}

	function submitRiskyChange() {
		allowRiskySubmit = true;
		showRiskyConfirm = false;
		settingsForm?.requestSubmit();
	}
</script>

<svelte:window onkeydown={handleKeydown} />

<form
	bind:this={settingsForm}
	method="POST"
	action="?/save"
	enctype="multipart/form-data"
	class="h-full flex flex-col"
	onsubmit={confirmRiskyChange}
	use:enhance={async ({ formData, cancel }) => {
		const logo = formData.get('logo');
		if (logo instanceof File && logo.size > 0) {
			if (!isSupportedImageFile(logo)) {
				toast.error(t('settings.logoInvalidType'));
				cancel();
				return;
			}
			const compressedLogo = await compressImage(logo, 1024, 0.85, { maxBytes: 700 * 1024, preservePng: true });
			if (compressedLogo.size > 5 * 1024 * 1024) {
				toast.error(t('settings.logoTooLarge'));
				cancel();
				return;
			}
			formData.set('logo', compressedLogo);
		}
		saving = true;
		return async ({ result, update }) => {
			try {
				if (result.type === 'error') {
					toast.error(formatEnhanceUnexpectedError(result.error, t));
					return;
				}
				await update();
			} finally {
				saving = false;
			}
		};
	}}
>
	<input type="hidden" name="updatedAt" value={settings.updatedAt ?? ''} />

	<div class="mb-lg">
		<PageHeader items={[{ label: t('common.console'), href: '/app' }, { label: t('nav.settings') }]} title={t('settings.title')} subtitle={t('settings.subtitle')}>
			{#snippet actions()}
				<!-- Standard PageHeader Actions (Desktop/Tablet >= sm) -->
				<div class="hidden sm:flex items-center gap-2 sm:gap-2.5">
					<!-- Reset Defaults: Restrained utility action -->
					<button
						type="button"
						class="h-10 px-3.5 rounded-button border border-stone/80 bg-white text-secondary hover:text-rose-700 hover:border-rose-300 hover:bg-rose-50/60 font-body-sm font-medium transition-all cursor-pointer shadow-2xs inline-flex items-center gap-1.5 active:scale-[0.98]"
						onclick={() => (showResetConfirm = true)}
						title={t('settings.resetDefaults')}
					>
						<span class="material-symbols-outlined text-[18px]">restart_alt</span>
						<span>{t('settings.resetDefaults')}</span>
					</button>

					<div class="h-4 w-px bg-stone/80 mx-0.5"></div>

					<!-- Cancel: Secondary Navigation -->
					<a
						href="/app"
						class="h-10 px-4 rounded-button border border-stone bg-white font-body-sm font-medium text-secondary hover:text-primary hover:bg-ash transition-all inline-flex items-center justify-center text-center shadow-2xs active:scale-[0.98]"
					>
						{t('settings.cancel')}
					</a>

					<!-- Save Changes: Dominant Primary Action -->
					<button
						type="submit"
						disabled={saving}
						class="h-10 px-5 rounded-button font-medium bg-ink text-white hover:bg-ink/90 active:scale-[0.98] transition-all cursor-pointer shadow-xs inline-flex items-center justify-center gap-2 disabled:opacity-60 disabled:cursor-not-allowed text-body-sm"
					>
						{#if saving}
							<svg class="w-4 h-4 animate-spin text-white" viewBox="0 0 24 24" fill="none">
								<circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
								<path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
							</svg>
							<span>{t('settings.saving')}</span>
						{:else}
							<span class="material-symbols-outlined text-[18px]">check</span>
							<span>{t('settings.save')}</span>
							<kbd class="hidden md:inline-flex items-center text-[10px] font-mono text-white/70 bg-white/15 px-1.5 py-0.5 rounded leading-none">
								{isMac ? '⌘S' : 'Ctrl+S'}
							</kbd>
						{/if}
					</button>
				</div>
			{/snippet}
		</PageHeader>
	</div>

	<!-- Categories Top Navigation Bar -->
	<nav class="flex items-center gap-1.5 overflow-x-auto pb-2 border-b border-stone scrollbar-none sticky top-16 z-20 bg-background/95 backdrop-blur-sm pt-1 mb-lg">
		<a
			href="#general"
			class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-caption font-semibold whitespace-nowrap transition-all shrink-0 cursor-pointer {activeSection === 'general' ? 'bg-primary text-white shadow-2xs' : 'text-secondary hover:text-primary hover:bg-ash'}"
			onclick={() => (activeSection = 'general')}
		>
			<span class="material-symbols-outlined text-[17px]">tune</span>
			<span>{t('settings.general')}</span>
		</a>
		<a
			href="#numbering"
			class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-caption font-semibold whitespace-nowrap transition-all shrink-0 cursor-pointer {activeSection === 'numbering' ? 'bg-primary text-white shadow-2xs' : 'text-secondary hover:text-primary hover:bg-ash'}"
			onclick={() => (activeSection = 'numbering')}
		>
			<span class="material-symbols-outlined text-[17px]">123</span>
			<span>{t('settings.ticketNumbering')}</span>
		</a>
		<a
			href="#operational"
			class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-caption font-semibold whitespace-nowrap transition-all shrink-0 cursor-pointer {activeSection === 'operational' ? 'bg-primary text-white shadow-2xs' : 'text-secondary hover:text-primary hover:bg-ash'}"
			onclick={() => (activeSection = 'operational')}
		>
			<span class="material-symbols-outlined text-[17px]">rule_settings</span>
			<span>{t('settings.operationalRules')}</span>
		</a>
		<div class="h-4 w-px bg-stone shrink-0 mx-1"></div>
		<a
			href="/app/settings/service-statuses"
			class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-caption font-medium whitespace-nowrap transition-all shrink-0 text-secondary hover:text-primary hover:bg-ash cursor-pointer"
		>
			<span class="material-symbols-outlined text-[17px]">account_tree</span>
			<span>{t('settings.serviceWorkflow')}</span>
		</a>
		<a
			href="/app/settings/service-templates"
			class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-caption font-medium whitespace-nowrap transition-all shrink-0 text-secondary hover:text-primary hover:bg-ash cursor-pointer"
		>
			<span class="material-symbols-outlined text-[17px]">checklist</span>
			<span>{t('settings.serviceTemplates')}</span>
		</a>
		<a
			href="/app/settings/print-templates"
			class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-caption font-medium whitespace-nowrap transition-all shrink-0 text-secondary hover:text-primary hover:bg-ash cursor-pointer"
		>
			<span class="material-symbols-outlined text-[17px]">print</span>
			<span>{t('settings.printTemplates')}</span>
		</a>
		<a
			href="/app/settings/notification-templates"
			class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-caption font-medium whitespace-nowrap transition-all shrink-0 text-secondary hover:text-primary hover:bg-ash cursor-pointer"
		>
			<span class="material-symbols-outlined text-[17px]">chat_bubble</span>
			<span>{t('settings.whatsappTemplates')}</span>
		</a>
	</nav>

	<!-- Settings Form Area -->
	<div class="w-full space-y-lg lg:space-y-xl pb-32 sm:pb-36">
		<!-- General Section -->
		<section data-bento class="scroll-mt-28 border border-stone rounded-xl p-lg bg-white shadow-2xs space-y-lg" id="general">
			<div class="border-b border-stone pb-md">
				<h3 class="font-subheading text-subheading text-primary font-semibold mb-0">{t('settings.generalSettings')}</h3>
				<p class="text-caption text-secondary mt-0.5">{t('settings.subtitle')}</p>
			</div>
			<div class="grid grid-cols-12 gap-lg lg:gap-xl">
				<div class="col-span-12 lg:col-span-8 space-y-lg">
					<div class="space-y-1 block">
						<label class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block" for="shopName">{t('settings.storeName')}</label>
						<input 
							id="shopName" 
							name="shopName" 
							class="w-full h-10 rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs" 
							type="text" 
							value={settings.shopName || ''} 
							required 
						/>
					</div>
					
					<div class="grid grid-cols-1 sm:grid-cols-2 gap-md">
						<div class="space-y-1 block">
							<label class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block" for="whatsAppNumber">{t('settings.whatsappPhone')}</label>
							<input 
								id="whatsAppNumber" 
								name="whatsAppNumber" 
								class="w-full h-10 rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary font-mono focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs" 
								type="tel" 
								value={settings.whatsAppNumber || ''} 
								required 
								oninput={handlePhoneInput}
								onpaste={handlePhonePaste}
							/>
						</div>
						
						<div class="space-y-1 block">
							<label class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block" for="phoneNumber">{t('settings.phoneNumber')}</label>
							<input 
								id="phoneNumber" 
								name="phoneNumber" 
								class="w-full h-10 rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary font-mono focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs" 
								type="tel" 
								value={settings.phoneNumber || ''} 
								required 
								oninput={handlePhoneInput}
								onpaste={handlePhonePaste}
							/>
						</div>
					</div>

					<div class="grid grid-cols-1 sm:grid-cols-2 gap-md">
						<div class="space-y-1 block">
							<label class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block" for="email">{t('settings.email')}</label>
							<input 
								id="email" 
								name="email" 
								class="w-full h-10 rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs" 
								type="email" 
								value={settings.email || ''} 
								required 
							/>
						</div>
						
						<div class="space-y-1 block">
							<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('settings.defaultLanguage')}</span>
							<Dropdown
								disabled={true}
								value={data.locale}
								options={locales.map((locale) => ({ value: locale.code, label: locale.label }))}
							/>
						</div>
					</div>

					<div class="space-y-1 block">
						<label class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block" for="currencyCode">{t('settings.currency')}</label>
						<Dropdown name="currencyCode" value={settings.currencyCode ?? 'IDR'} options={[
							{ value: 'IDR', label: t('settings.currencyIdr') },
							{ value: 'USD', label: t('settings.currencyUsd') },
							{ value: 'SGD', label: t('settings.currencySgd') },
							{ value: 'EUR', label: t('settings.currencyEur') },
							{ value: 'MYR', label: t('settings.currencyMyr') },
							{ value: 'GBP', label: t('settings.currencyGbp') },
							{ value: 'AUD', label: t('settings.currencyAud') },
						]} />
					</div>

					<!-- Inventory Pricing Defaults Box -->
					<div class="rounded-xl border border-stone bg-ash/30 p-md space-y-md">
						<div>
							<h4 class="font-subheading text-body font-semibold text-primary">{t('settings.inventoryPricing')}</h4>
							<p class="text-caption text-secondary mt-0.5">{t('inventory.settingsHint') ?? 'Configure pricing calculation, tax, and decimal policies.'}</p>
						</div>
						<div class="grid grid-cols-1 sm:grid-cols-3 gap-md">
							<label class="space-y-1 block">
								<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('settings.inventoryDefaultTaxPercent')}</span>
								<input name="inventoryDefaultTaxPercent" type="number" min="0" max="100" step="0.01" value={settings.inventoryDefaultTaxPercent ?? 11} class="w-full h-10 rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary" />
							</label>
							<label class="space-y-1 block">
								<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('settings.inventoryRoundingIncrement')}</span>
								<input name="inventoryRoundingIncrement" type="number" min="1" step="1" value={settings.inventoryRoundingIncrement ?? 1000} class="w-full h-10 rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary" />
							</label>
							<label class="space-y-1 block">
								<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('settings.inventoryDefaultUnit')}</span>
								<input name="inventoryDefaultUnit" value={settings.inventoryDefaultUnit ?? 'pcs'} class="w-full h-10 rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary" />
							</label>
						</div>
						<div class="flex flex-wrap gap-md text-body-sm text-secondary pt-1">
							<label class="flex items-center gap-sm cursor-pointer select-none">
								<input name="inventoryAllowDecimalQuantity" type="checkbox" checked={settings.inventoryAllowDecimalQuantity ?? true} class="rounded border-stone text-primary focus:ring-primary" />
								<span class="font-medium text-primary text-body-sm">{t('settings.inventoryAllowDecimalQuantity')}</span>
							</label>
							<label class="flex items-center gap-sm cursor-pointer select-none">
								<input name="inventoryAutoCreateCategoryOnImport" type="checkbox" checked={settings.inventoryAutoCreateCategoryOnImport ?? false} class="rounded border-stone text-primary focus:ring-primary" />
								<span class="font-medium text-primary text-body-sm">{t('settings.inventoryAutoCreateCategoryOnImport')}</span>
							</label>
						</div>
					</div>

					<div class="space-y-1 block">
						<label class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block" for="address">{t('settings.storeAddress')}</label>
						<textarea 
							id="address" 
							name="address" 
							class="w-full rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs" 
							rows="3" 
							required
						>{settings.address || ''}</textarea>
					</div>

					<div class="space-y-1 block">
						<label class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block" for="receiptFooter">{t('settings.receiptFooter')}</label>
						<textarea 
							id="receiptFooter" 
							name="receiptFooter" 
							class="w-full rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs" 
							rows="3"
						>{settings.receiptFooter || ''}</textarea>
					</div>
				</div>

				<!-- Logo Upload Card -->
				<div class="col-span-12 lg:col-span-4">
					<div data-bento class="border border-stone rounded-xl p-md sm:p-lg bg-ash/30 lg:bg-white lg:shadow-2xs space-y-md lg:sticky lg:top-28">
						<label class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block" for="logo">{t('settings.storeLogo')}</label>
						<input type="hidden" name="logoUrl" value={settings.logoUrl ?? ''} />
						<label for="logo" class="w-full aspect-square max-w-[240px] sm:max-w-[280px] mx-auto bg-white/80 border-2 border-dashed border-stone rounded-xl flex flex-col items-center justify-center text-center p-md group cursor-pointer hover:border-action-blue hover:bg-focus-blue/10 transition-colors">
							{#if logoPreviewUrl}
								<img src={logoPreviewUrl} alt={t('settings.storeLogo')} class="max-h-32 max-w-32 object-contain mb-sm" />
								<p class="font-body-sm text-body-sm font-semibold text-primary">{t('settings.replaceLogo')}</p>
							{:else}
								<div class="w-16 h-16 bg-white border border-stone rounded-lg mb-sm flex items-center justify-center shadow-2xs">
									<span class="material-symbols-outlined text-secondary text-[28px]">image</span>
								</div>
								<p class="font-body-sm text-body-sm font-semibold text-primary">{t('settings.noLogo')}</p>
							{/if}
							{#if selectedLogoName}
								<p class="text-caption text-primary font-medium mt-xs truncate max-w-full">{selectedLogoName}</p>
							{/if}
							<p class="text-caption text-secondary mt-1 whitespace-pre-line">{t('settings.logoHint')}</p>
						</label>
						<input id="logo" name="logo" class="sr-only" type="file" accept="image/png,image/jpeg,image/webp" onchange={handleLogoChange} />
						<p class="text-caption text-secondary text-center">{t('settings.logoUsageHint')}</p>
					</div>
				</div>
			</div>
		</section>

		<!-- Operational Rules Section -->
		<section data-bento class="scroll-mt-28 border border-stone rounded-xl p-lg bg-white shadow-2xs space-y-lg" id="operational">
			<div class="border-b border-stone pb-md">
				<h3 class="font-subheading text-subheading text-primary font-semibold mb-0">{t('settings.operationalRules')}</h3>
				<p class="mt-xs text-body-sm text-secondary">{t('settings.operationalRulesHint')}</p>
			</div>
			<div class="grid grid-cols-1 md:grid-cols-2 gap-md">
				<label class="flex items-start gap-sm rounded-xl border border-stone bg-ash/30 p-md text-body-sm text-secondary hover:bg-ash/60 transition-colors cursor-pointer select-none">
					<input class="mt-0.5 rounded border-stone text-primary focus:ring-primary" name="requirePhotoEvidenceOnIntake" type="checkbox" checked={settings.requirePhotoEvidenceOnIntake ?? false} />
					<div>
						<span class="font-semibold text-primary block">{t('settings.requirePhotoEvidenceOnIntake')}</span>
						<span class="text-caption text-secondary block mt-0.5">{t('settings.ruleRequirePhotoHint')}</span>
					</div>
				</label>
				<label class="flex items-start gap-sm rounded-xl border border-stone bg-ash/30 p-md text-body-sm text-secondary hover:bg-ash/60 transition-colors cursor-pointer select-none">
					<input class="mt-0.5 rounded border-stone text-primary focus:ring-primary" name="showPhotoEvidenceOnPublicTracking" type="checkbox" checked={settings.showPhotoEvidenceOnPublicTracking ?? false} />
					<div>
						<span class="font-semibold text-primary block">{t('settings.showPhotoEvidenceOnPublicTracking')}</span>
						<span class="text-caption text-secondary block mt-0.5">{t('settings.rulePublicPhotoHint')}</span>
					</div>
				</label>
				<label class="flex items-start gap-sm rounded-xl border border-stone bg-ash/30 p-md text-body-sm text-secondary hover:bg-ash/60 transition-colors cursor-pointer select-none">
					<input class="mt-0.5 rounded border-stone text-primary focus:ring-primary" name="requireApprovalEstimate" type="checkbox" checked={settings.requireApprovalEstimate ?? false} />
					<div>
						<span class="font-semibold text-primary block">{t('settings.requireApprovalEstimate')}</span>
						<span class="text-caption text-secondary block mt-0.5">{t('settings.ruleApprovalEstimateHint')}</span>
					</div>
				</label>
				<label class="flex items-start gap-sm rounded-xl border border-stone bg-ash/30 p-md text-body-sm text-secondary hover:bg-ash/60 transition-colors cursor-pointer select-none">
					<input class="mt-0.5 rounded border-stone text-primary focus:ring-primary" name="allowUnpaidPickupOverride" type="checkbox" checked={settings.allowUnpaidPickupOverride ?? true} />
					<div>
						<span class="font-semibold text-primary block">{t('settings.allowUnpaidPickupOverride')}</span>
						<span class="text-caption text-secondary block mt-0.5">{t('settings.ruleUnpaidPickupHint')}</span>
					</div>
				</label>
			</div>

			<div class="rounded-xl border border-stone bg-ash/30 p-md space-y-md">
				<div>
					<h4 class="font-subheading text-body font-semibold text-primary">{t('settings.operationalHoursTitle')}</h4>
					<p class="text-caption text-secondary mt-0.5">{t('settings.operationalHoursHint')}</p>
				</div>
				<div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5 gap-md">
					<div class="flex flex-col gap-1.5 min-w-0">
						<label for="settings-open-time" class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block truncate">{t('settings.operationalOpenTime')}</label>
						<TimePicker id="settings-open-time" name="operationalOpenTime" value={settings.operationalOpenTime ?? '09:00'} required />
					</div>
					<div class="flex flex-col gap-1.5 min-w-0">
						<label for="settings-close-time" class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block truncate">{t('settings.operationalCloseTime')}</label>
						<TimePicker id="settings-close-time" name="operationalCloseTime" value={settings.operationalCloseTime ?? '18:00'} required />
					</div>
					<label class="flex flex-col gap-1.5 min-w-0">
						<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block truncate">{t('settings.operationalDefaultBreakMinutes')}</span>
						<input name="operationalDefaultBreakMinutes" type="number" min="0" max="480" value={settings.operationalDefaultBreakMinutes ?? 60} class="w-full h-10 rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary box-border" required />
					</label>
					<label class="flex flex-col gap-1.5 min-w-0">
						<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block truncate">{t('settings.operationalRequiredDaysWeek')}</span>
						<input name="operationalRequiredWorkingDaysPerWeek" type="number" min="1" max="7" value={settings.operationalRequiredWorkingDaysPerWeek ?? 6} class="w-full h-10 rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary box-border" required />
					</label>
					<label class="flex flex-col gap-1.5 min-w-0">
						<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block truncate">{t('settings.operationalRequiredDaysMonth')}</span>
						<input name="operationalRequiredWorkingDaysPerMonth" type="number" min="1" max="31" value={settings.operationalRequiredWorkingDaysPerMonth ?? 26} class="w-full h-10 rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary box-border" required />
					</label>
				</div>
				<div class="pt-3 border-t border-stone/60">
					<label class="space-y-1 block max-w-md">
						<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block whitespace-nowrap">{t('settings.operationalTimeZone')}</span>
						<Dropdown
							name="operationalTimeZone"
							value={settings.operationalTimeZone ?? ''}
							options={[
								{ value: 'Asia/Jakarta', label: 'Asia/Jakarta (WIB · UTC+7 · Jakarta, Java, Sumatra)' },
								{ value: 'Asia/Makassar', label: 'Asia/Makassar (WITA · UTC+8 · Bali, Makassar, Balikpapan)' },
								{ value: 'Asia/Jayapura', label: 'Asia/Jayapura (WIT · UTC+9 · Jayapura, Maluku)' },
								{ value: 'Asia/Singapore', label: 'Asia/Singapore (SGT · UTC+8 · Singapore)' },
								{ value: 'UTC', label: 'UTC (UTC+0 · Universal Time Coordinated)' }
							]}
						/>
						<p class="text-[11px] text-secondary mt-1">{t('settings.operationalTimeZoneHint')}</p>
					</label>
				</div>
			</div>
		</section>

		<!-- Ticket Numbering Section -->
		<section data-bento class="scroll-mt-28 border border-stone rounded-xl p-lg bg-white shadow-2xs space-y-lg" id="numbering">
			<div class="border-b border-stone pb-md">
				<h3 class="font-subheading text-subheading text-primary font-semibold mb-0">{t('settings.ticketNumbering')}</h3>
				<p class="text-caption text-secondary mt-0.5">Customize prefix, sequential counters, and serial format codes.</p>
			</div>
			<div class="grid grid-cols-1 lg:grid-cols-12 gap-lg lg:gap-xl items-start">
				<div class="lg:col-span-7 space-y-lg">
					<div class="grid grid-cols-1 sm:grid-cols-2 gap-md">
						<div class="space-y-1 block">
							<label class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block" for="ticketPrefix">{t('settings.globalPrefix')}</label>
							<input 
								id="ticketPrefix" 
								name="ticketPrefix" 
								bind:value={ticketPrefix} 
								class="w-full h-10 rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary font-mono uppercase focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs" 
								type="text" 
								required 
							/>
						</div>
						<div class="space-y-1 block">
							<span class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">{t('settings.ticketDateFormat')}</span>
							<Dropdown 
								name="ticketDateFormat" 
								bind:value={ticketDateFormat}
								options={[
									{ value: 'yyyyMMdd', label: t('settings.ticketDateFormat.yyyyMMdd') },
									{ value: 'yyyyMM', label: t('settings.ticketDateFormat.yyyyMM') },
									{ value: 'yyMMdd', label: t('settings.ticketDateFormat.yyMMdd') }
								]}
							/>
						</div>
					</div>
					<div class="grid grid-cols-1 sm:grid-cols-2 gap-md">
						<div class="space-y-1 block">
							<label class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block" for="startNumber">{t('settings.startNumber')}</label>
							<input id="startNumber" name="ticketStartNumber" class="w-full h-10 rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary font-mono focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs" type="number" min="1" max="999999" bind:value={ticketStartNumber} />
						</div>
						<div class="space-y-1 block">
							<label class="text-label-caps font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block" for="minDigits">{t('settings.minDigits')}</label>
							<input id="minDigits" name="ticketMinDigits" class="w-full h-10 rounded-input border border-stone bg-white px-3 py-2 text-body-sm text-primary font-mono focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary shadow-2xs" type="number" min="1" max="10" bind:value={ticketMinDigits} />
						</div>
					</div>
					<p class="text-caption text-secondary">{t('settings.ticketNumberingWarning')}</p>
				</div>
				<!-- Live Preview Card -->
				<div class="lg:col-span-5">
					<div data-bento class="bg-ash/50 border border-stone rounded-xl p-lg flex flex-col justify-center items-center relative overflow-hidden min-h-[180px] shadow-2xs">
						<div class="absolute top-3 left-3">
							<span class="text-[10px] font-bold text-secondary uppercase tracking-wider bg-white border border-stone/60 px-2 py-0.5 rounded-full">{t('settings.livePreview')}</span>
						</div>
						<div class="text-center space-y-sm z-10 pt-2">
							<div class="text-[26px] sm:text-[30px] font-mono font-bold tracking-widest text-primary break-all">
								{ticketPrefix ? ticketPrefix.toUpperCase() : 'SVC'}-{todayStamp}-{String(ticketStartNumber || 1).padStart(ticketMinDigits || 4, '0')}
							</div>
							<p class="text-caption text-secondary">{t('settings.nextTicketFormat')}</p>
						</div>
					</div>
				</div>
			</div>
		</section>
	</div>

	<!-- Sticky Floating Action Dock (Always on mobile; appears on desktop when scrolled past header) -->
	<aside
		class="fixed inset-x-3 sm:inset-x-auto sm:right-6 lg:right-10 bottom-4 z-40 transition-all duration-300 ease-out {isScrolled ? 'opacity-100 translate-y-0 pointer-events-auto' : 'max-sm:opacity-100 max-sm:translate-y-0 max-sm:pointer-events-auto opacity-0 translate-y-3 pointer-events-none'}"
		aria-label="Settings actions dock"
	>
		<div class="flex items-center justify-between sm:justify-end gap-2 sm:gap-2.5 rounded-2xl border border-stone/90 bg-white/95 p-1.5 sm:p-2 shadow-lg backdrop-blur-md ring-1 ring-black/5">
			<!-- Reset Defaults: Restrained utility action -->
			<button
				type="button"
				class="h-9 sm:h-10 px-2.5 sm:px-3.5 rounded-button border border-stone/80 bg-white text-secondary hover:text-rose-700 hover:border-rose-300 hover:bg-rose-50/60 font-body-sm font-medium transition-all cursor-pointer shadow-2xs inline-flex items-center gap-1.5 active:scale-[0.98] text-xs sm:text-body-sm"
				onclick={() => (showResetConfirm = true)}
				title={t('settings.resetDefaults')}
			>
				<span class="material-symbols-outlined text-[17px] sm:text-[18px]">restart_alt</span>
				<span class="hidden sm:inline">{t('settings.resetDefaults')}</span>
			</button>

			<div class="h-4 w-px bg-stone/80 mx-0.5 hidden sm:block"></div>

			<!-- Cancel: Secondary Navigation -->
			<a
				href="/app"
				class="h-9 sm:h-10 px-3 sm:px-4 rounded-button border border-stone bg-white font-body-sm font-medium text-secondary hover:text-primary hover:bg-ash transition-all inline-flex items-center justify-center text-center shadow-2xs active:scale-[0.98] text-xs sm:text-body-sm"
			>
				{t('settings.cancel')}
			</a>

			<!-- Save Changes: Dominant Primary Action -->
			<button
				type="submit"
				disabled={saving}
				class="h-9 sm:h-10 px-3.5 sm:px-5 rounded-button font-medium bg-ink text-white hover:bg-ink/90 active:scale-[0.98] transition-all cursor-pointer shadow-xs inline-flex items-center justify-center gap-1.5 sm:gap-2 disabled:opacity-60 disabled:cursor-not-allowed text-xs sm:text-body-sm"
			>
				{#if saving}
					<svg class="w-3.5 h-3.5 sm:w-4 sm:h-4 animate-spin text-white" viewBox="0 0 24 24" fill="none">
						<circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
						<path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
					</svg>
					<span>{t('settings.saving')}</span>
				{:else}
					<span class="material-symbols-outlined text-[17px] sm:text-[18px]">check</span>
					<span>{t('settings.save')}</span>
					<kbd class="hidden md:inline-flex items-center text-[10px] font-mono text-white/70 bg-white/15 px-1.5 py-0.5 rounded leading-none">
						{isMac ? '⌘S' : 'Ctrl+S'}
					</kbd>
				{/if}
			</button>
		</div>
	</aside>
</form>

{#if showRiskyConfirm}
	<div class="fixed inset-0 z-[60] flex items-center justify-center bg-black/40 backdrop-blur-xs px-md py-8" role="presentation">
		<div class="w-[min(620px,calc(100vw-2rem))] max-h-[calc(100vh-2rem)] overflow-auto rounded-xl border border-stone bg-white p-lg sm:p-xl shadow-xl" role="dialog" aria-modal="true" aria-labelledby="settings-risky-title">
			<div class="space-y-lg">
				<div class="flex items-start gap-md">
					<div class="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-amber-50 border border-amber-200 text-amber-600">
						<span class="material-symbols-outlined text-[22px]">warning</span>
					</div>
					<div class="min-w-0">
						<h3 id="settings-risky-title" class="font-subheading text-subheading font-semibold text-primary">{t('settings.ticketNumberingConfirmTitle')}</h3>
						<p class="mt-sm text-body-sm text-secondary">{t('settings.ticketNumberingConfirm')}</p>
					</div>
				</div>
				<div class="flex flex-wrap justify-end gap-sm border-t border-stone pt-md">
					<button class="px-button-x py-button-y rounded-button border border-stone bg-white font-body-sm font-medium text-secondary hover:text-primary hover:bg-ash transition-colors shadow-2xs cursor-pointer" type="button" onclick={() => (showRiskyConfirm = false)}>{t('common.cancel')}</button>
					<button class="h-10 px-5 rounded-button font-medium bg-primary text-white hover:opacity-90 transition-opacity shadow-2xs cursor-pointer inline-flex items-center gap-2 disabled:opacity-60 disabled:cursor-not-allowed" type="button" onclick={submitRiskyChange} disabled={saving}>
						{#if saving}
							<svg class="w-4 h-4 animate-spin text-white" viewBox="0 0 24 24" fill="none">
								<circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
								<path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
							</svg>
							<span>{t('settings.saving')}</span>
						{:else}
							<span>{t('settings.save')}</span>
						{/if}
					</button>
				</div>
			</div>
		</div>
	</div>
{/if}

{#if showResetConfirm}
	<div class="fixed inset-0 z-[60] flex items-center justify-center bg-black/40 backdrop-blur-xs px-md py-8" role="presentation">
		<div class="w-[min(620px,calc(100vw-2rem))] max-h-[calc(100vh-2rem)] overflow-auto rounded-xl border border-stone bg-white p-lg sm:p-xl shadow-xl" role="dialog" aria-modal="true" aria-labelledby="settings-reset-title">
			<div class="space-y-lg">
				<div class="flex items-start gap-md">
					<div class="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-rose-50 border border-rose-200 text-rose-600">
						<span class="material-symbols-outlined text-[22px]">restart_alt</span>
					</div>
					<div class="min-w-0">
						<h3 id="settings-reset-title" class="font-subheading text-subheading font-semibold text-primary">{t('settings.resetDefaultsTitle')}</h3>
						<p class="mt-sm text-body-sm text-secondary">{t('settings.resetDefaultsMessage')}</p>
					</div>
				</div>
				<div class="flex flex-wrap justify-end gap-sm border-t border-stone pt-md">
					<button class="px-button-x py-button-y rounded-button border border-stone bg-white font-body-sm font-medium text-secondary hover:text-primary hover:bg-ash transition-colors shadow-2xs cursor-pointer" type="button" onclick={() => (showResetConfirm = false)}>{t('common.cancel')}</button>
					<form
						method="POST"
						action="?/resetDefaults"
						use:enhance={() => {
							resetting = true;
							return async ({ result, update }) => {
								try {
									if (result.type === 'error') {
										toast.error(formatEnhanceUnexpectedError(result.error, t));
										return;
									}
									await update();
								} finally {
									resetting = false;
									showResetConfirm = false;
								}
							};
						}}
					>
						<button class="h-10 px-5 rounded-button font-medium bg-rose-600 hover:bg-rose-700 text-white transition-colors shadow-2xs cursor-pointer inline-flex items-center gap-2 disabled:opacity-60 disabled:cursor-not-allowed" type="submit" disabled={resetting}>
							{#if resetting}
								<svg class="w-4 h-4 animate-spin text-white" viewBox="0 0 24 24" fill="none">
									<circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
									<path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
								</svg>
							{/if}
							<span>{t('settings.resetDefaults')}</span>
						</button>
					</form>
				</div>
			</div>
		</div>
	</div>
{/if}
