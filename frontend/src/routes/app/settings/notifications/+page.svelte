<script lang="ts">
	import { enhance } from '$app/forms';
	import { goto } from '$app/navigation';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import { createTranslator, formatDate } from '$lib/i18n';
	import { toast } from '$lib/stores/toast.svelte';
	import type { WhatsAppProviderType } from '$lib/types/notifications';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	const channels = $derived(form?.channels ?? data.channels);
	const rules = $derived(form?.rules ?? data.rules);

	let activeTab = $state<'channels' | 'rules' | 'webhooks' | 'deliveries'>('channels');
	let busy = $state(false);
	let useSystemSmtp = $state(true);
	let waProvider = $state<WhatsAppProviderType>('Manual');
	let testEmailTo = $state('');
	let testWhatsAppTo = $state('');
	let copiedSecret = $state(false);

	$effect(() => {
		activeTab = data.tab as 'channels' | 'rules' | 'webhooks' | 'deliveries';
	});

	$effect(() => {
		useSystemSmtp = channels.useSystemSmtp;
		waProvider = channels.whatsAppProvider;
	});

	function selectTab(next: 'channels' | 'rules' | 'webhooks' | 'deliveries') {
		activeTab = next;
		const params = new URLSearchParams(typeof window !== 'undefined' ? window.location.search : '');
		params.set('tab', next);
		goto(`/app/settings/notifications?${params.toString()}`, { replaceState: true, keepFocus: true, noScroll: true });
	}

	function copySecret(secret: string) {
		if (typeof navigator === 'undefined' || !navigator.clipboard) return;
		navigator.clipboard.writeText(secret).then(() => {
			copiedSecret = true;
			toast.success(t('notifications.secretCopied'));
			setTimeout(() => (copiedSecret = false), 2500);
		});
	}

	function eventLabel(eventKey: string): string {
		const key = `notifications.event.${eventKey}`;
		const translated = t(key);
		return translated === key ? eventKey : translated;
	}

	function statusBadgeClass(status: string): string {
		switch (status) {
			case 'Sent':
				return 'bg-emerald-100 text-emerald-800';
			case 'Failed':
				return 'bg-red-100 text-red-800';
			case 'Skipped':
				return 'bg-[#f4f0ea] text-[#6b6158]';
			default:
				return 'bg-amber-100 text-amber-900';
		}
	}
</script>

<svelte:head>
	<title>{t('notifications.pageTitle')} · {t('brand.name')}</title>
</svelte:head>

<div class="space-y-lg pb-16">
	<PageHeader
		items={[
			{ label: t('nav.settings'), href: '/app/settings' },
			{ label: t('notifications.pageTitle') }
		]}
		title={t('notifications.pageTitle')}
		subtitle={t('notifications.pageSubtitle')}
	/>

	<!-- Tabs -->
	<nav class="flex items-center gap-1.5 overflow-x-auto border-b border-[#e6dfd5] pb-2">
		<button
			type="button"
			onclick={() => selectTab('channels')}
			class="inline-flex items-center gap-1.5 px-3.5 py-2 rounded-lg text-xs font-semibold whitespace-nowrap transition cursor-pointer {activeTab === 'channels'
				? 'bg-[#1c1815] text-white'
				: 'text-[#6b6158] hover:text-[#1c1815] hover:bg-[#f4f0ea]'}"
		>
			<span class="material-symbols-outlined text-[16px]">hub</span>
			<span>{t('notifications.tabChannels')}</span>
		</button>
		<button
			type="button"
			onclick={() => selectTab('rules')}
			class="inline-flex items-center gap-1.5 px-3.5 py-2 rounded-lg text-xs font-semibold whitespace-nowrap transition cursor-pointer {activeTab === 'rules'
				? 'bg-[#1c1815] text-white'
				: 'text-[#6b6158] hover:text-[#1c1815] hover:bg-[#f4f0ea]'}"
		>
			<span class="material-symbols-outlined text-[16px]">tune</span>
			<span>{t('notifications.tabRules')}</span>
		</button>
		<button
			type="button"
			onclick={() => selectTab('webhooks')}
			class="inline-flex items-center gap-1.5 px-3.5 py-2 rounded-lg text-xs font-semibold whitespace-nowrap transition cursor-pointer {activeTab === 'webhooks'
				? 'bg-[#1c1815] text-white'
				: 'text-[#6b6158] hover:text-[#1c1815] hover:bg-[#f4f0ea]'}"
		>
			<span class="material-symbols-outlined text-[16px]">webhook</span>
			<span>{t('notifications.tabWebhooks')} ({data.webhooks.length})</span>
		</button>
		<button
			type="button"
			onclick={() => selectTab('deliveries')}
			class="inline-flex items-center gap-1.5 px-3.5 py-2 rounded-lg text-xs font-semibold whitespace-nowrap transition cursor-pointer {activeTab === 'deliveries'
				? 'bg-[#1c1815] text-white'
				: 'text-[#6b6158] hover:text-[#1c1815] hover:bg-[#f4f0ea]'}"
		>
			<span class="material-symbols-outlined text-[16px]">history</span>
			<span>{t('notifications.tabDeliveries')}</span>
		</button>
	</nav>

	<!-- TAB 1: CHANNELS & SECURITY POLICIES -->
	{#if activeTab === 'channels'}
		{#if !channels.canStoreSecrets}
			<div class="rounded-xl border border-amber-300 bg-amber-50 p-4 text-xs text-amber-950 flex items-start gap-2.5">
				<span class="material-symbols-outlined text-[18px] text-amber-700 shrink-0">warning</span>
				<span>{t('notifications.secretKeyMissingWarning')}</span>
			</div>
		{/if}

		<form
			method="POST"
			action="?/saveChannels"
			class="space-y-6"
			use:enhance={() => {
				busy = true;
				return async ({ update }) => {
					await update({ reset: false });
					busy = false;
				};
			}}
		>
			<!-- Email (SMTP) -->
			<section class="rounded-2xl border border-[#e6dfd5] bg-white p-6 shadow-2xs space-y-4">
				<div class="flex flex-wrap items-center justify-between gap-3 border-b border-[#e6dfd5] pb-3.5">
					<div>
						<h2 class="text-base font-bold text-[#1c1815] flex items-center gap-2">
							<span class="material-symbols-outlined text-[19px] text-[#b45039]">mail</span>
							<span>{t('notifications.emailSectionTitle')}</span>
						</h2>
						<p class="text-xs text-[#6b6158] mt-0.5">{t('notifications.emailSectionDesc')}</p>
					</div>
					<label class="inline-flex items-center gap-2 text-xs font-semibold text-[#1c1815] cursor-pointer">
						<input type="checkbox" name="emailEnabled" checked={channels.emailEnabled} class="w-4 h-4 accent-[#1c1815]" />
						<span>{t('notifications.channelEnabled')}</span>
					</label>
				</div>

				<div class="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-[#faf8f5] border border-[#e6dfd5] px-4 py-3">
					<label class="inline-flex items-center gap-2 text-xs font-semibold text-[#1c1815] cursor-pointer">
						<input
							type="checkbox"
							name="useSystemSmtp"
							bind:checked={useSystemSmtp}
							class="w-4 h-4 accent-[#1c1815]"
						/>
						<span>{t('notifications.useSystemSmtp')}</span>
					</label>
					<span class="text-[11px] font-medium px-2.5 py-0.5 rounded-full {channels.systemSmtpConfigured ? 'bg-emerald-100 text-emerald-800' : 'bg-[#f4f0ea] text-[#6b6158]'}">
						{channels.systemSmtpConfigured ? t('notifications.systemSmtpReady') : t('notifications.systemSmtpNotConfigured')}
					</span>
				</div>

				{#if !useSystemSmtp}
					<div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
						<div class="space-y-1">
							<label for="smtpHost" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">{t('notifications.smtpHost')}</label>
							<input id="smtpHost" name="smtpHost" type="text" value={channels.smtpHost ?? ''} placeholder="smtp.resend.com" class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 text-sm" />
						</div>
						<div class="space-y-1">
							<label for="smtpPort" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">{t('notifications.smtpPort')}</label>
							<input id="smtpPort" name="smtpPort" type="number" value={channels.smtpPort || 587} class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 text-sm font-mono" />
						</div>
						<div class="flex items-end pb-2">
							<label class="inline-flex items-center gap-2 text-xs font-medium text-[#1c1815] cursor-pointer">
								<input type="checkbox" name="smtpUseSsl" checked={channels.smtpUseSsl} class="w-4 h-4 accent-[#1c1815]" />
								<span>{t('notifications.smtpUseSsl')}</span>
							</label>
						</div>
						<div class="space-y-1">
							<label for="smtpUsername" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">{t('notifications.smtpUsername')}</label>
							<input id="smtpUsername" name="smtpUsername" type="text" value={channels.smtpUsername ?? ''} class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 text-sm" />
						</div>
						<div class="space-y-1">
							<label for="smtpPassword" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">
								{t('notifications.smtpPassword')}
								{#if channels.hasSmtpPassword && channels.smtpPasswordLast4}
									<span class="font-mono text-emerald-700 ml-1">(•••{channels.smtpPasswordLast4})</span>
								{/if}
							</label>
							<input id="smtpPassword" name="smtpPassword" type="password" placeholder={channels.hasSmtpPassword ? t('notifications.leaveBlankKeep') : ''} class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 text-sm" />
							{#if channels.hasSmtpPassword}
								<label class="inline-flex items-center gap-1.5 text-[11px] text-[#6b6158] mt-1 cursor-pointer">
									<input type="checkbox" name="clearSmtpPassword" class="w-3.5 h-3.5" />
									<span>{t('notifications.clearSavedSecret')}</span>
								</label>
							{/if}
						</div>
						<div class="space-y-1">
							<label for="fromEmail" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">{t('notifications.fromEmail')}</label>
							<input id="fromEmail" name="fromEmail" type="email" value={channels.fromEmail ?? ''} placeholder="noreply@yourdomain.com" class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 text-sm" />
						</div>
					</div>
				{/if}

				<div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
					<div class="space-y-1">
						<label for="fromName" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">{t('notifications.fromName')}</label>
						<input id="fromName" name="fromName" type="text" value={channels.fromName ?? ''} placeholder="Servora Service Desk" class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 text-sm" />
					</div>
					<div class="space-y-1">
						<label for="staffNotificationEmails" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">{t('notifications.staffEmails')}</label>
						<input id="staffNotificationEmails" name="staffNotificationEmails" type="text" value={channels.staffNotificationEmails ?? ''} placeholder="ops@shop.com, manager@shop.com" class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 text-sm" />
					</div>
				</div>
			</section>

			<!-- WhatsApp -->
			<section class="rounded-2xl border border-[#e6dfd5] bg-white p-6 shadow-2xs space-y-4">
				<div class="flex flex-wrap items-center justify-between gap-3 border-b border-[#e6dfd5] pb-3.5">
					<div>
						<h2 class="text-base font-bold text-[#1c1815] flex items-center gap-2">
							<span class="material-symbols-outlined text-[19px] text-emerald-700">chat</span>
							<span>{t('notifications.whatsappSectionTitle')}</span>
						</h2>
						<p class="text-xs text-[#6b6158] mt-0.5">{t('notifications.whatsappSectionDesc')}</p>
					</div>
					<label class="inline-flex items-center gap-2 text-xs font-semibold text-[#1c1815] cursor-pointer">
						<input type="checkbox" name="whatsAppEnabled" checked={channels.whatsAppEnabled} class="w-4 h-4 accent-[#1c1815]" />
						<span>{t('notifications.channelEnabled')}</span>
					</label>
				</div>

				<div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
					<div class="space-y-1">
						<label for="whatsAppProvider" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">{t('notifications.whatsappProvider')}</label>
						<select id="whatsAppProvider" name="whatsAppProvider" bind:value={waProvider} class="w-full h-10 rounded-lg border border-[#e6dfd5] bg-white px-3 text-sm">
							<option value="Manual">{t('notifications.waProviderManual')}</option>
							<option value="MetaCloud">Meta WhatsApp Cloud API (Official)</option>
							<option value="Fonnte">Fonnte (Indonesia)</option>
							<option value="Waha">WAHA (Self-Hosted)</option>
							<option value="CustomHttp">{t('notifications.waProviderCustom')}</option>
						</select>
					</div>

					{#if waProvider !== 'Manual'}
						<div class="space-y-1">
							<label for="whatsAppApiToken" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">
								{t('notifications.apiToken')}
								{#if channels.hasWhatsAppApiToken && channels.whatsAppApiTokenLast4}
									<span class="font-mono text-emerald-700 ml-1">(•••{channels.whatsAppApiTokenLast4})</span>
								{/if}
							</label>
							<input id="whatsAppApiToken" name="whatsAppApiToken" type="password" placeholder={channels.hasWhatsAppApiToken ? t('notifications.leaveBlankKeep') : ''} class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 text-sm" />
							{#if channels.hasWhatsAppApiToken}
								<label class="inline-flex items-center gap-1.5 text-[11px] text-[#6b6158] mt-1 cursor-pointer">
									<input type="checkbox" name="clearWhatsAppApiToken" class="w-3.5 h-3.5" />
									<span>{t('notifications.clearSavedSecret')}</span>
								</label>
							{/if}
						</div>
					{/if}
				</div>

				{#if waProvider === 'MetaCloud'}
					<div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
						<div class="space-y-1">
							<label for="whatsAppPhoneNumberId" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">{t('notifications.metaPhoneNumberId')}</label>
							<input id="whatsAppPhoneNumberId" name="whatsAppPhoneNumberId" type="text" value={channels.whatsAppPhoneNumberId ?? ''} placeholder="123456789012345" class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 text-sm font-mono" />
						</div>
						<div class="space-y-1">
							<label for="whatsAppApiUrl" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">{t('notifications.apiUrlOptional')}</label>
							<input id="whatsAppApiUrl" name="whatsAppApiUrl" type="url" value={channels.whatsAppApiUrl ?? ''} placeholder="https://graph.facebook.com/v21.0" class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 text-sm" />
						</div>
					</div>
				{:else if waProvider === 'Fonnte' || waProvider === 'Waha' || waProvider === 'CustomHttp'}
					<div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
						<div class="space-y-1">
							<label for="whatsAppApiUrl" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">{t('notifications.apiUrl')}</label>
							<input
								id="whatsAppApiUrl"
								name="whatsAppApiUrl"
								type="url"
								value={channels.whatsAppApiUrl ?? ''}
								placeholder={waProvider === 'Fonnte' ? 'https://api.fonnte.com/send' : waProvider === 'Waha' ? 'https://waha.yourdomain.com/api/sendText' : 'https://gateway.example.com/send'}
								class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 text-sm"
							/>
						</div>
						{#if waProvider === 'Waha'}
							<div class="space-y-1">
								<label for="whatsAppPhoneNumberId" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">{t('notifications.wahaSession')}</label>
								<input id="whatsAppPhoneNumberId" name="whatsAppPhoneNumberId" type="text" value={channels.whatsAppPhoneNumberId ?? 'default'} class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 text-sm font-mono" />
							</div>
						{:else if waProvider === 'CustomHttp'}
							<div class="space-y-1">
								<label for="whatsAppAuthHeaderName" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">{t('notifications.authHeaderName')}</label>
								<input id="whatsAppAuthHeaderName" name="whatsAppAuthHeaderName" type="text" value={channels.whatsAppAuthHeaderName ?? 'Authorization'} class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 text-sm font-mono" />
							</div>
						{/if}
					</div>
					{#if waProvider === 'CustomHttp'}
						<div class="space-y-1">
							<label for="whatsAppCustomPayloadTemplate" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">{t('notifications.customPayloadTemplate')}</label>
							<textarea
								id="whatsAppCustomPayloadTemplate"
								name="whatsAppCustomPayloadTemplate"
								rows="3"
								placeholder={'{"to":"{{phone}}","message":"{{message}}"}'}
								class="w-full rounded-lg border border-[#e6dfd5] p-3 text-xs font-mono"
							>{channels.whatsAppCustomPayloadTemplate ?? ''}</textarea>
						</div>
					{/if}
				{/if}
			</section>

			<!-- Telegram Bot -->
			<section class="rounded-2xl border border-[#e6dfd5] bg-white p-6 shadow-2xs space-y-4">
				<div class="flex flex-wrap items-center justify-between gap-3 border-b border-[#e6dfd5] pb-3.5">
					<div>
						<h2 class="text-base font-bold text-[#1c1815] flex items-center gap-2">
							<span class="material-symbols-outlined text-[19px] text-sky-700">send</span>
							<span>{t('notifications.telegramSectionTitle')}</span>
						</h2>
						<p class="text-xs text-[#6b6158] mt-0.5">{t('notifications.telegramSectionDesc')}</p>
					</div>
					<label class="inline-flex items-center gap-2 text-xs font-semibold text-[#1c1815] cursor-pointer">
						<input type="checkbox" name="telegramEnabled" checked={channels.telegramEnabled} class="w-4 h-4 accent-[#1c1815]" />
						<span>{t('notifications.channelEnabled')}</span>
					</label>
				</div>

				<div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
					<div class="space-y-1">
						<label for="telegramBotToken" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">
							{t('notifications.telegramBotToken')}
							{#if channels.hasTelegramBotToken && channels.telegramBotTokenLast4}
								<span class="font-mono text-emerald-700 ml-1">(•••{channels.telegramBotTokenLast4})</span>
							{/if}
						</label>
						<input id="telegramBotToken" name="telegramBotToken" type="password" placeholder={channels.hasTelegramBotToken ? t('notifications.leaveBlankKeep') : '123456:ABC-DEF...'} class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 text-sm font-mono" />
						{#if channels.hasTelegramBotToken}
							<label class="inline-flex items-center gap-1.5 text-[11px] text-[#6b6158] mt-1 cursor-pointer">
								<input type="checkbox" name="clearTelegramBotToken" class="w-3.5 h-3.5" />
								<span>{t('notifications.clearSavedSecret')}</span>
							</label>
						{/if}
					</div>
					<div class="space-y-1">
						<label for="telegramChatId" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">{t('notifications.telegramChatId')}</label>
						<input id="telegramChatId" name="telegramChatId" type="text" value={channels.telegramChatId ?? ''} placeholder="-1001234567890" class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 text-sm font-mono" />
					</div>
					<div class="space-y-1">
						<label for="telegramThreadId" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">{t('notifications.telegramThreadId')}</label>
						<input id="telegramThreadId" name="telegramThreadId" type="number" value={channels.telegramThreadId ?? ''} placeholder="Optional topic ID" class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 text-sm font-mono" />
					</div>
				</div>
			</section>

			<!-- Security & OTP Policies -->
			<section class="rounded-2xl border border-[#e6dfd5] bg-white p-6 shadow-2xs space-y-4">
				<div class="border-b border-[#e6dfd5] pb-3.5">
					<h2 class="text-base font-bold text-[#1c1815] flex items-center gap-2">
						<span class="material-symbols-outlined text-[19px] text-[#b45039]">shield_lock</span>
						<span>{t('notifications.securityPoliciesTitle')}</span>
					</h2>
					<p class="text-xs text-[#6b6158] mt-0.5">{t('notifications.securityPoliciesDesc')}</p>
				</div>

				<div class="grid grid-cols-1 md:grid-cols-2 gap-5">
					<div class="space-y-1.5">
						<label for="twoFactorEnforcement" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">
							{t('notifications.twoFactorPolicyLabel')}
						</label>
						<select id="twoFactorEnforcement" name="twoFactorEnforcement" value={channels.twoFactorEnforcement} class="w-full h-10 rounded-lg border border-[#e6dfd5] bg-white px-3 text-sm">
							<option value="Optional">{t('notifications.twoFactorPolicyOptional')}</option>
							<option value="AdminsOnly">{t('notifications.twoFactorPolicyAdminsOnly')}</option>
							<option value="AllStaff">{t('notifications.twoFactorPolicyAllStaff')}</option>
						</select>
						<p class="text-[11px] text-[#6b6158]">{t('notifications.twoFactorPolicyHelp')}</p>
					</div>

					<div class="rounded-xl border border-[#e6dfd5] bg-[#faf8f5] p-4 flex items-start gap-3">
						<input
							id="requireOtpForPublicQuoteApproval"
							type="checkbox"
							name="requireOtpForPublicQuoteApproval"
							checked={channels.requireOtpForPublicQuoteApproval}
							class="mt-1 w-4 h-4 accent-[#1c1815]"
						/>
						<label for="requireOtpForPublicQuoteApproval" class="space-y-1 cursor-pointer">
							<span class="block text-xs font-semibold text-[#1c1815]">{t('notifications.quoteOtpToggleLabel')}</span>
							<span class="block text-[11px] text-[#6b6158]">{t('notifications.quoteOtpToggleDesc')}</span>
						</label>
					</div>
				</div>
			</section>

			<div class="flex justify-end">
				<button
					type="submit"
					disabled={busy}
					class="inline-flex items-center gap-2 rounded-xl bg-[#1c1815] px-5 py-2.5 text-sm font-semibold text-white hover:opacity-90 cursor-pointer disabled:opacity-50"
				>
					<span class="material-symbols-outlined text-[18px]">check</span>
					<span>{t('settings.save')}</span>
				</button>
			</div>
		</form>

		<!-- Quick Test Bar -->
		<section class="rounded-2xl border border-[#e6dfd5] bg-[#faf8f5] p-5 space-y-4">
			<h3 class="text-sm font-bold text-[#1c1815]">{t('notifications.testChannelsTitle')}</h3>
			<div class="grid grid-cols-1 md:grid-cols-3 gap-3">
				<form method="POST" action="?/testChannel" use:enhance class="flex gap-2">
					<input type="hidden" name="channel" value="Email" />
					<input name="recipient" type="email" bind:value={testEmailTo} placeholder="test@example.com" required class="flex-1 min-w-0 h-9 rounded-lg border border-[#e6dfd5] bg-white px-3 text-xs" />
					<button type="submit" class="shrink-0 rounded-lg border border-[#1c1815] bg-white px-3 h-9 text-xs font-semibold text-[#1c1815] hover:bg-[#f4f0ea] cursor-pointer">
						{t('notifications.testEmailBtn')}
					</button>
				</form>
				<form method="POST" action="?/testChannel" use:enhance class="flex gap-2">
					<input type="hidden" name="channel" value="WhatsApp" />
					<input name="recipient" type="tel" bind:value={testWhatsAppTo} placeholder="628123456789" required class="flex-1 min-w-0 h-9 rounded-lg border border-[#e6dfd5] bg-white px-3 text-xs font-mono" />
					<button type="submit" class="shrink-0 rounded-lg border border-[#1c1815] bg-white px-3 h-9 text-xs font-semibold text-[#1c1815] hover:bg-[#f4f0ea] cursor-pointer">
						{t('notifications.testWhatsAppBtn')}
					</button>
				</form>
				<form method="POST" action="?/testChannel" use:enhance class="flex">
					<input type="hidden" name="channel" value="Telegram" />
					<button type="submit" class="w-full rounded-lg border border-[#1c1815] bg-white px-3 h-9 text-xs font-semibold text-[#1c1815] hover:bg-[#f4f0ea] cursor-pointer">
						{t('notifications.testTelegramBtn')}
					</button>
				</form>
			</div>
		</section>
	{/if}

	<!-- TAB 2: ROUTING RULES MATRIX -->
	{#if activeTab === 'rules'}
		<form
			method="POST"
			action="?/saveRules"
			class="rounded-2xl border border-[#e6dfd5] bg-white shadow-2xs overflow-hidden"
			use:enhance={() => {
				busy = true;
				return async ({ update }) => {
					await update({ reset: false });
					busy = false;
				};
			}}
		>
			<div class="p-5 border-b border-[#e6dfd5] flex flex-wrap items-center justify-between gap-3">
				<div>
					<h2 class="text-base font-bold text-[#1c1815]">{t('notifications.rulesMatrixTitle')}</h2>
					<p class="text-xs text-[#6b6158] mt-0.5">{t('notifications.rulesMatrixDesc')}</p>
				</div>
				<button
					type="submit"
					disabled={busy}
					class="inline-flex items-center gap-1.5 rounded-xl bg-[#1c1815] px-4 py-2 text-xs font-semibold text-white hover:opacity-90 cursor-pointer disabled:opacity-50"
				>
					<span class="material-symbols-outlined text-[16px]">check</span>
					<span>{t('settings.save')}</span>
				</button>
			</div>

			<div class="overflow-x-auto">
				<table class="w-full text-left text-xs">
					<thead class="bg-[#faf8f5] border-b border-[#e6dfd5] text-[#6b6158] uppercase text-[10px] tracking-wider">
						<tr>
							<th class="px-5 py-3 font-bold">{t('notifications.colEvent')}</th>
							<th class="px-3 py-3 text-center font-bold">{t('notifications.colCustomerEmail')}</th>
							<th class="px-3 py-3 text-center font-bold">{t('notifications.colCustomerWhatsApp')}</th>
							<th class="px-3 py-3 text-center font-bold">{t('notifications.colStaffEmail')}</th>
							<th class="px-3 py-3 text-center font-bold">{t('notifications.colStaffTelegram')}</th>
							<th class="px-3 py-3 text-center font-bold">{t('notifications.colWebhook')}</th>
						</tr>
					</thead>
					<tbody class="divide-y divide-[#e6dfd5]/70">
						{#each rules as rule (rule.eventType)}
							<tr class="hover:bg-[#faf8f5]/60">
								<td class="px-5 py-3">
									<input type="hidden" name="eventType" value={rule.eventType} />
									<input type="hidden" name={`eventKey_${rule.eventType}`} value={rule.eventKey} />
									<div class="font-semibold text-[#1c1815]">{eventLabel(rule.eventKey)}</div>
									<div class="font-mono text-[10px] text-[#6b6158]">{rule.eventKey}</div>
								</td>
								<td class="px-3 py-3 text-center">
									<input type="checkbox" name={`custEmail_${rule.eventType}`} checked={rule.sendCustomerEmail} class="w-4 h-4 accent-[#1c1815]" />
								</td>
								<td class="px-3 py-3 text-center">
									<input type="checkbox" name={`custWa_${rule.eventType}`} checked={rule.sendCustomerWhatsApp} class="w-4 h-4 accent-[#1c1815]" />
								</td>
								<td class="px-3 py-3 text-center">
									<input type="checkbox" name={`staffEmail_${rule.eventType}`} checked={rule.sendStaffEmail} class="w-4 h-4 accent-[#1c1815]" />
								</td>
								<td class="px-3 py-3 text-center">
									<input type="checkbox" name={`staffTg_${rule.eventType}`} checked={rule.sendStaffTelegram} class="w-4 h-4 accent-[#1c1815]" />
								</td>
								<td class="px-3 py-3 text-center">
									<input type="checkbox" name={`webhook_${rule.eventType}`} checked={rule.sendWebhook} class="w-4 h-4 accent-[#1c1815]" />
								</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>
		</form>
	{/if}

	<!-- TAB 3: OUTBOUND WEBHOOKS -->
	{#if activeTab === 'webhooks'}
		{#if form?.revealedSecret}
			<section class="rounded-2xl border-2 border-[#b45039]/40 bg-[#faf8f5] p-5 space-y-2.5">
				<div class="flex flex-wrap items-center justify-between gap-2">
					<h3 class="text-sm font-bold text-[#1c1815]">
						{t('notifications.signingSecretRevealTitle')}: {form.revealedSecret.name}
					</h3>
					<button
						type="button"
						onclick={() => copySecret(form?.revealedSecret?.secret ?? '')}
						class="inline-flex items-center gap-1 rounded-lg border border-[#e6dfd5] bg-white px-3 py-1.5 text-xs font-semibold text-[#1c1815] hover:bg-[#f4f0ea] cursor-pointer"
					>
						<span class="material-symbols-outlined text-[15px]">{copiedSecret ? 'check' : 'content_copy'}</span>
						<span>{copiedSecret ? t('common.copied') : t('notifications.copySecret')}</span>
					</button>
				</div>
				<p class="text-xs text-[#6b6158]">{t('notifications.signingSecretRevealNote')}</p>
				<code class="block rounded-lg border border-[#e6dfd5] bg-white px-3 py-2 font-mono text-xs text-[#1c1815] break-all select-all">
					{form.revealedSecret.secret}
				</code>
			</section>
		{/if}

		<!-- Add New Webhook -->
		<form
			method="POST"
			action="?/createWebhook"
			class="rounded-2xl border border-[#e6dfd5] bg-white p-6 shadow-2xs space-y-4"
			use:enhance={() => {
				busy = true;
				return async ({ update }) => {
					await update();
					busy = false;
				};
			}}
		>
			<div>
				<h2 class="text-base font-bold text-[#1c1815]">{t('notifications.addWebhookTitle')}</h2>
				<p class="text-xs text-[#6b6158] mt-0.5">{t('notifications.addWebhookDesc')}</p>
			</div>

			<div class="grid grid-cols-1 md:grid-cols-2 gap-4">
				<div class="space-y-1">
					<label for="wh-name" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">{t('notifications.webhookName')}</label>
					<input id="wh-name" name="name" type="text" required placeholder="n8n / ERP Sync" class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 text-sm" />
				</div>
				<div class="space-y-1">
					<label for="wh-url" class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">{t('notifications.webhookUrl')}</label>
					<input id="wh-url" name="targetUrl" type="url" required placeholder="https://hooks.example.com/servora" class="w-full h-10 rounded-lg border border-[#e6dfd5] px-3 text-sm font-mono" />
				</div>
			</div>

			<div class="space-y-2">
				<span class="block text-[11px] font-bold uppercase tracking-wider text-[#6b6158]">{t('notifications.webhookEvents')}</span>
				<div class="flex flex-wrap gap-2">
					{#each rules as rule (rule.eventKey)}
						<label class="inline-flex items-center gap-1.5 rounded-lg border border-[#e6dfd5] bg-[#faf8f5] px-2.5 py-1.5 text-xs cursor-pointer">
							<input type="checkbox" name="subscribedEvents" value={rule.eventKey} checked class="w-3.5 h-3.5 accent-[#1c1815]" />
							<span class="font-mono text-[11px]">{rule.eventKey}</span>
						</label>
					{/each}
				</div>
			</div>

			<div class="flex flex-wrap items-center justify-between gap-3 pt-2">
				<label class="inline-flex items-center gap-2 text-xs font-semibold text-[#1c1815] cursor-pointer">
					<input type="checkbox" name="isActive" checked class="w-4 h-4 accent-[#1c1815]" />
					<span>{t('notifications.webhookActive')}</span>
				</label>
				<button
					type="submit"
					disabled={busy}
					class="inline-flex items-center gap-1.5 rounded-xl bg-[#1c1815] px-4 py-2 text-xs font-semibold text-white hover:opacity-90 cursor-pointer disabled:opacity-50"
				>
					<span class="material-symbols-outlined text-[16px]">add</span>
					<span>{t('notifications.addWebhookBtn')}</span>
				</button>
			</div>
		</form>

		<!-- Existing Webhooks -->
		<div class="space-y-3">
			{#if data.webhooks.length === 0}
				<div class="rounded-2xl border border-dashed border-[#e6dfd5] bg-white p-8 text-center text-xs text-[#6b6158]">
					{t('notifications.noWebhooks')}
				</div>
			{:else}
				{#each data.webhooks as wh (wh.id)}
					<div class="rounded-2xl border border-[#e6dfd5] bg-white p-5 shadow-2xs space-y-3">
						<div class="flex flex-wrap items-start justify-between gap-3">
							<div class="space-y-1 min-w-0">
								<div class="flex items-center gap-2">
									<h3 class="text-sm font-bold text-[#1c1815]">{wh.name}</h3>
									<span class="rounded-full px-2 py-0.5 text-[10px] font-semibold {wh.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-[#f4f0ea] text-[#6b6158]'}">
										{wh.isActive ? t('common.active') : t('common.inactive')}
									</span>
									{#if wh.lastStatusCode}
										<span class="font-mono text-[10px] rounded px-1.5 py-0.5 bg-[#faf8f5] border border-[#e6dfd5] text-[#6b6158]">
											HTTP {wh.lastStatusCode}
										</span>
									{/if}
								</div>
								<p class="font-mono text-xs text-[#6b6158] truncate">{wh.targetUrl}</p>
								<p class="text-[11px] text-[#6b6158]">
									{t('notifications.signingSecretLabel')}: <code class="font-mono">whsec_•••{wh.signingSecretLast4}</code>
									{#if wh.lastTriggeredAt}
										· {t('notifications.lastTriggered')}: {formatDate(wh.lastTriggeredAt, data.locale)}
									{/if}
								</p>
							</div>

							<div class="flex flex-wrap items-center gap-2">
								<form method="POST" action="?/testWebhook" use:enhance>
									<input type="hidden" name="id" value={wh.id} />
									<button type="submit" class="rounded-lg border border-[#e6dfd5] bg-[#faf8f5] px-2.5 py-1.5 text-xs font-semibold text-[#1c1815] hover:bg-[#f4f0ea] cursor-pointer">
										{t('notifications.pingBtn')}
									</button>
								</form>
								<form method="POST" action="?/rotateWebhookSecret" use:enhance>
									<input type="hidden" name="id" value={wh.id} />
									<input type="hidden" name="name" value={wh.name} />
									<button type="submit" class="rounded-lg border border-[#e6dfd5] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#1c1815] hover:bg-[#f4f0ea] cursor-pointer">
										{t('notifications.rotateSecretBtn')}
									</button>
								</form>
								<form method="POST" action="?/toggleWebhook" use:enhance>
									<input type="hidden" name="id" value={wh.id} />
									<input type="hidden" name="name" value={wh.name} />
									<input type="hidden" name="targetUrl" value={wh.targetUrl} />
									<input type="hidden" name="nextActive" value={wh.isActive ? 'false' : 'true'} />
									<input type="hidden" name="subscribedEventsJson" value={JSON.stringify(wh.subscribedEvents)} />
									<button type="submit" class="rounded-lg border border-[#e6dfd5] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#1c1815] hover:bg-[#f4f0ea] cursor-pointer">
										{wh.isActive ? t('notifications.pauseBtn') : t('notifications.enableBtn')}
									</button>
								</form>
								<form method="POST" action="?/deleteWebhook" use:enhance>
									<input type="hidden" name="id" value={wh.id} />
									<button type="submit" class="rounded-lg border border-red-200 bg-white px-2.5 py-1.5 text-xs font-semibold text-red-700 hover:bg-red-50 cursor-pointer">
										{t('common.delete')}
									</button>
								</form>
							</div>
						</div>

						<div class="flex flex-wrap gap-1.5">
							{#each wh.subscribedEvents as ev}
								<span class="rounded bg-[#f4f0ea] px-2 py-0.5 font-mono text-[10px] text-[#6b6158]">{ev}</span>
							{/each}
						</div>
					</div>
				{/each}
			{/if}
		</div>
	{/if}

	<!-- TAB 4: DELIVERY LOG -->
	{#if activeTab === 'deliveries'}
		<section class="rounded-2xl border border-[#e6dfd5] bg-white shadow-2xs overflow-hidden">
			<form method="GET" class="p-4 border-b border-[#e6dfd5] bg-[#faf8f5] flex flex-wrap items-center gap-3">
				<input type="hidden" name="tab" value="deliveries" />
				<select name="channel" value={data.filters.channel} class="h-9 rounded-lg border border-[#e6dfd5] bg-white px-3 text-xs">
					<option value="">{t('notifications.filterAllChannels')}</option>
					<option value="Email">Email</option>
					<option value="WhatsApp">WhatsApp</option>
					<option value="Telegram">Telegram</option>
					<option value="Webhook">Webhook</option>
				</select>
				<select name="status" value={data.filters.status} class="h-9 rounded-lg border border-[#e6dfd5] bg-white px-3 text-xs">
					<option value="">{t('notifications.filterAllStatuses')}</option>
					<option value="Sent">Sent</option>
					<option value="Failed">Failed</option>
					<option value="Pending">Pending</option>
					<option value="Skipped">Skipped</option>
				</select>
				<button type="submit" class="rounded-lg bg-[#1c1815] px-3.5 h-9 text-xs font-semibold text-white hover:opacity-90 cursor-pointer">
					{t('common.apply')}
				</button>
			</form>

			<div class="overflow-x-auto">
				<table class="w-full text-left text-xs">
					<thead class="bg-[#faf8f5] border-b border-[#e6dfd5] text-[#6b6158] uppercase text-[10px] tracking-wider">
						<tr>
							<th class="px-4 py-3 font-bold">{t('notifications.colTime')}</th>
							<th class="px-4 py-3 font-bold">{t('notifications.colChannel')}</th>
							<th class="px-4 py-3 font-bold">{t('notifications.colEvent')}</th>
							<th class="px-4 py-3 font-bold">{t('notifications.colRecipient')}</th>
							<th class="px-4 py-3 font-bold">{t('notifications.colStatus')}</th>
							<th class="px-4 py-3 text-right font-bold">{t('common.actions')}</th>
						</tr>
					</thead>
					<tbody class="divide-y divide-[#e6dfd5]/70">
						{#if data.deliveries.items.length === 0}
							<tr>
								<td colspan="6" class="px-4 py-8 text-center text-[#6b6158]">{t('notifications.noDeliveries')}</td>
							</tr>
						{:else}
							{#each data.deliveries.items as item (item.id)}
								<tr class="hover:bg-[#faf8f5]/60">
									<td class="px-4 py-3 whitespace-nowrap text-[#6b6158]">{formatDate(item.createdAt, data.locale)}</td>
									<td class="px-4 py-3 whitespace-nowrap">
										<div class="font-semibold text-[#1c1815]">{item.channel}</div>
										<div class="text-[10px] text-[#6b6158]">{item.provider}</div>
									</td>
									<td class="px-4 py-3">
										<div class="font-mono text-[11px] text-[#1c1815]">{item.eventType}</div>
										{#if item.subject}
											<div class="text-[11px] text-[#6b6158] truncate max-w-xs">{item.subject}</div>
										{/if}
									</td>
									<td class="px-4 py-3 font-mono text-[11px] text-[#1c1815] max-w-[200px] truncate">{item.recipient}</td>
									<td class="px-4 py-3">
										<span class="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-semibold {statusBadgeClass(item.status)}">
											{item.status}
											{#if item.httpStatusCode}
												<span>({item.httpStatusCode})</span>
											{/if}
										</span>
										{#if item.errorMessage}
											<div class="text-[10px] text-red-700 mt-0.5 max-w-xs truncate" title={item.errorMessage}>{item.errorMessage}</div>
										{/if}
									</td>
									<td class="px-4 py-3 text-right whitespace-nowrap">
										{#if item.status === 'Failed'}
											<form method="POST" action="?/retryDelivery" use:enhance class="inline">
												<input type="hidden" name="id" value={item.id} />
												<button type="submit" class="rounded-lg border border-[#e6dfd5] bg-white px-2.5 py-1 text-[11px] font-semibold text-[#1c1815] hover:bg-[#f4f0ea] cursor-pointer">
													{t('notifications.retryBtn')}
												</button>
											</form>
										{:else}
											<span class="text-[11px] text-[#6b6158]">#{item.attemptCount}</span>
										{/if}
									</td>
								</tr>
							{/each}
						{/if}
					</tbody>
				</table>
			</div>
		</section>
	{/if}
</div>
