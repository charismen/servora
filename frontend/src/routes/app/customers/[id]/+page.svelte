<script lang="ts">
	import { enhance } from '$app/forms';
	import PageHeader from '$lib/components/PageHeader.svelte';
	import DeleteConfirmButton from '$lib/components/DeleteConfirmButton.svelte';
	import { can, permissions } from '$lib/auth/permissions';
	import type { PageData, ActionData } from './$types';
	import type { Device } from '$lib/types/devices';
	import { createTranslator, formatDate } from '$lib/i18n';
	import { fade } from 'svelte/transition';

	let { data, form }: { data: PageData; form: ActionData } = $props();
	const t = $derived(createTranslator(data.locale));
	let customer = $derived(data.customer);
	let devices = $derived(data.devices ?? []);
	let serviceHistory = $derived(data.serviceHistory ?? []);
	let deviceHistories = $derived(data.deviceHistories ?? {});
	let repeatSummary = $derived(data.repeatSummary);
	const canWriteCustomer = $derived(can(data.user.permissions, permissions.customersWrite));
	const canDeleteCustomer = $derived(can(data.user.permissions, permissions.customersDelete));
	const canWriteDevice = $derived(can(data.user.permissions, permissions.devicesWrite));
	const canDeleteDevice = $derived(can(data.user.permissions, permissions.devicesDelete));

	let showBanModal = $state(false);
	let showDeviceModal = $state(false);
	let editingDevice = $state<Device | null>(null);
	let copiedPhone = $state(false);

	function openCreateDevice() {
		editingDevice = null;
		showDeviceModal = true;
	}

	function openEditDevice(device: Device) {
		editingDevice = device;
		showDeviceModal = true;
	}

	function closeDeviceModal() {
		showDeviceModal = false;
		editingDevice = null;
	}

	function historyForDevice(deviceId: number) {
		return deviceHistories[deviceId] ?? [];
	}

	function copyPhone(phone: string) {
		if (navigator?.clipboard) {
			void navigator.clipboard.writeText(phone);
			copiedPhone = true;
			setTimeout(() => {
				copiedPhone = false;
			}, 2000);
		}
	}

	function getWhatsAppUrl(phone: string): string {
		let clean = phone.replace(/\D/g, '');
		if (clean.startsWith('0')) {
			clean = '62' + clean.slice(1);
		}
		return `https://wa.me/${clean}`;
	}

	function getStatusPillClasses(status: string) {
		const s = status.toLowerCase();
		if (s.includes('done') || s.includes('complete') || s.includes('resolved') || s.includes('paid')) {
			return 'border-emerald-300 bg-emerald-500/10 text-emerald-800';
		}
		if (s.includes('cancel') || s.includes('fail') || s.includes('banned')) {
			return 'border-rose-300 bg-rose-500/10 text-rose-800';
		}
		if (s.includes('wait') || s.includes('hold') || s.includes('part')) {
			return 'border-amber-300 bg-amber-500/10 text-amber-800';
		}
		if (s.includes('progress') || s.includes('repair') || s.includes('diagnos')) {
			return 'border-blue-300 bg-blue-500/10 text-blue-800';
		}
		return 'border-stone bg-ash text-secondary';
	}
</script>

<div class="space-y-lg pb-xl">
	<PageHeader
		items={[
			{ label: t('common.console'), href: '/app' },
			{ label: t('nav.customers'), href: '/app/customers' },
			{ label: customer.fullName }
		]}
		title={customer.fullName}
		subtitle={`ID: C-${customer.id} · ${customer.phoneNumber}`}
	>
		{#snippet actions()}
			<div class="flex items-center gap-2">
				<!-- WhatsApp Action -->
				<a
					href={getWhatsAppUrl(customer.phoneNumber)}
					target="_blank"
					rel="noopener noreferrer"
					class="inline-flex h-9 items-center gap-1.5 rounded-button border border-stone bg-white px-3 text-body-sm font-semibold text-emerald-700 hover:bg-ash transition-colors shadow-2xs"
					title={t('customers.list.openWhatsApp')}
				>
					<span class="material-symbols-outlined text-[17px]">chat</span>
					<span class="hidden sm:inline">WhatsApp</span>
				</a>

				<!-- Edit Profile Action -->
				{#if canWriteCustomer}
					<a
						href="/app/customers/{customer.id}/edit"
						class="inline-flex h-9 items-center gap-1.5 rounded-button border border-stone bg-white px-3.5 text-body-sm font-semibold text-ink hover:bg-ash transition-colors shadow-2xs"
					>
						<span class="material-symbols-outlined text-[17px]">edit</span>
						<span>{t('customers.form.editProfile')}</span>
					</a>
				{/if}

				<!-- Delete Customer Action -->
				{#if canDeleteCustomer}
					<form method="POST" action="?/delete">
						<DeleteConfirmButton
							label={t('common.delete')}
							confirmTitle={t('common.deleteConfirmTitle')}
							confirmMessage={t('common.deleteConfirm')}
							cancelLabel={t('common.cancel')}
							class="inline-flex h-9 items-center justify-center rounded-button border border-error/30 bg-error/10 px-3 text-body-sm font-semibold text-error hover:bg-error/20 transition-colors shadow-2xs cursor-pointer"
						/>
					</form>
				{/if}
			</div>
		{/snippet}
	</PageHeader>

	<!-- Repeat Customer Metric Cards -->
	{#if repeatSummary}
		<div class="grid grid-cols-1 sm:grid-cols-3 gap-md">
			<div data-bento class="bg-surface-container-lowest border border-stone rounded-xl p-4 shadow-2xs flex items-center justify-between">
				<div>
					<span class="font-label-caps text-secondary text-[11px] font-bold tracking-wider uppercase block mb-1">
						{t('customers.history.total')}
					</span>
					<div class="font-mono text-[24px] font-bold text-ink tracking-tight">
						{repeatSummary.totalTickets}
					</div>
				</div>
				<div class="w-10 h-10 rounded-xl bg-ash border border-stone flex items-center justify-center text-ink shrink-0">
					<span class="material-symbols-outlined text-[20px]">confirmation_number</span>
				</div>
			</div>

			<div data-bento class="bg-surface-container-lowest border border-stone rounded-xl p-4 shadow-2xs flex items-center justify-between">
				<div>
					<span class="font-label-caps text-secondary text-[11px] font-bold tracking-wider uppercase block mb-1">
						{t('customers.history.active')}
					</span>
					<div class="font-mono text-[24px] font-bold text-ink tracking-tight">
						{repeatSummary.activeTickets}
					</div>
				</div>
				<div class="w-10 h-10 rounded-xl bg-ash border border-stone flex items-center justify-center text-ink shrink-0">
					<span class="material-symbols-outlined text-[20px]">build</span>
				</div>
			</div>

			<div data-bento class="bg-surface-container-lowest border border-stone rounded-xl p-4 shadow-2xs flex items-center justify-between">
				<div>
					<span class="font-label-caps text-secondary text-[11px] font-bold tracking-wider uppercase block mb-1">
						{t('customers.history.completed')}
					</span>
					<div class="font-mono text-[24px] font-bold text-emerald-700 tracking-tight">
						{repeatSummary.completedTickets}
					</div>
				</div>
				<div class="w-10 h-10 rounded-xl bg-emerald-500/10 border border-emerald-300 text-emerald-800 flex items-center justify-center shrink-0">
					<span class="material-symbols-outlined text-[20px]">check_circle</span>
				</div>
			</div>
		</div>
	{/if}

	<!-- Main Grid: Content (2/3) + Sidebar (1/3) -->
	<div class="grid grid-cols-1 lg:grid-cols-12 gap-lg items-start">
		<!-- Left Main Column (8 cols) -->
		<div class="lg:col-span-8 space-y-lg">
			<!-- Contact Information Card -->
			<section data-bento class="bg-white border border-stone rounded-2xl p-6 shadow-2xs space-y-4">
				<div class="flex items-center justify-between border-b border-stone/60 pb-3">
					<div class="flex items-center gap-2">
						<span class="material-symbols-outlined text-secondary text-[20px]">badge</span>
						<h3 class="font-subheading text-[15px] font-bold text-ink">{t('customers.detail.contactDetails')}</h3>
					</div>
					<div>
						{#if customer.isBanned}
							<span class="inline-flex items-center gap-1.5 rounded-full border border-rose-300 bg-rose-500/10 px-2.5 py-0.5 text-[11px] font-semibold text-rose-800">
								<span class="w-1.5 h-1.5 rounded-full bg-rose-600"></span>
								<span>{t('customers.list.status.banned')}</span>
							</span>
						{:else if customer.isStarred}
							<span class="inline-flex items-center gap-1.5 rounded-full border border-amber-300 bg-amber-500/10 px-2.5 py-0.5 text-[11px] font-semibold text-amber-800">
								<span class="material-symbols-outlined text-[13px] fill-icon text-amber-600">star</span>
								<span>{t('customers.list.status.starred')}</span>
							</span>
						{:else}
							<span class="inline-flex items-center gap-1.5 rounded-full border border-emerald-300 bg-emerald-500/10 px-2.5 py-0.5 text-[11px] font-semibold text-emerald-800">
								<span class="w-1.5 h-1.5 rounded-full bg-emerald-600"></span>
								<span>{t('customers.list.status.active')}</span>
							</span>
						{/if}
					</div>
				</div>

				<div class="grid grid-cols-1 sm:grid-cols-2 gap-4 text-body-sm">
					<!-- Phone Number -->
					<div class="rounded-xl border border-stone/80 bg-surface-container-low/40 p-3.5 space-y-1">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">
							{t('customers.form.phoneNumber')}
						</span>
						<div class="flex items-center gap-2">
							<strong class="text-ink font-mono text-body-base">{customer.phoneNumber}</strong>
							<button
								type="button"
								onclick={() => copyPhone(customer.phoneNumber)}
								class="text-secondary hover:text-ink transition-colors p-0.5 cursor-pointer"
								title={copiedPhone ? t('customers.list.phoneCopied') : t('customers.list.copyPhone')}
							>
								<span class="material-symbols-outlined text-[16px]">
									{copiedPhone ? 'check' : 'content_copy'}
								</span>
							</button>
						</div>
					</div>

					<!-- Email Address -->
					<div class="rounded-xl border border-stone/80 bg-surface-container-low/40 p-3.5 space-y-1">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">
							{t('customers.form.email')}
						</span>
						<span class="text-ink font-mono text-body-base block truncate">
							{customer.email || '—'}
						</span>
					</div>

					<!-- Physical Address -->
					<div class="sm:col-span-2 rounded-xl border border-stone/80 bg-surface-container-low/40 p-3.5 space-y-1">
						<span class="font-label-caps text-secondary text-[11px] font-bold uppercase tracking-wider block">
							{t('customers.detail.address')}
						</span>
						<p class="text-ink text-body-sm whitespace-pre-line m-0">
							{customer.address || '—'}
						</p>
					</div>
				</div>
			</section>

			<!-- Internal Notes Card -->
			<section data-bento class="bg-white border border-stone rounded-2xl p-6 shadow-2xs space-y-3">
				<div class="flex items-center gap-2 border-b border-stone/60 pb-3">
					<span class="material-symbols-outlined text-secondary text-[20px]">sticky_note_2</span>
					<h3 class="font-subheading text-[15px] font-bold text-ink">{t('customers.detail.internalNotesHistory')}</h3>
				</div>
				<p class="whitespace-pre-line text-body-sm text-secondary m-0 leading-relaxed min-h-[48px]">
					{customer.notes || t('customers.detail.noNotes')}
				</p>
			</section>

			<!-- Registered Devices Section -->
			<section data-bento class="bg-white border border-stone rounded-2xl p-6 shadow-2xs space-y-4">
				<div class="flex flex-wrap items-center justify-between gap-md border-b border-stone/60 pb-3">
					<div>
						<h3 class="font-subheading text-[15px] font-bold text-ink">{t('customers.detail.devices')}</h3>
						<p class="text-xs text-secondary mt-0.5">{t('customers.detail.devicesSubtitle')}</p>
					</div>
					{#if canWriteDevice}
						<button
							type="button"
							onclick={openCreateDevice}
							class="inline-flex h-8 items-center gap-1.5 rounded-button bg-ink px-3 text-xs font-semibold text-white hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer"
						>
							<span class="material-symbols-outlined text-[16px]">add</span>
							<span>{t('customers.detail.addDevice')}</span>
						</button>
					{/if}
				</div>

				{#if devices.length === 0}
					<div class="p-8 text-center rounded-xl border border-dashed border-stone bg-ash/20">
						<span class="material-symbols-outlined text-[32px] text-secondary mb-1">devices</span>
						<p class="text-body-sm text-secondary m-0">{t('customers.detail.noDevices')}</p>
					</div>
				{:else}
					<div class="space-y-3">
						{#each devices as device (device.id)}
							<div class="rounded-xl border border-stone bg-white p-4 shadow-2xs space-y-3 hover:border-stone/90 transition-all">
								<div class="flex items-start justify-between gap-md">
									<div class="flex items-center gap-3">
										<div class="w-9 h-9 rounded-xl bg-surface-container-low border border-stone flex items-center justify-center text-ink shrink-0">
											<span class="material-symbols-outlined text-[19px]">devices</span>
										</div>
										<div>
											<strong class="block text-ink font-semibold text-body-sm">
												{device.brand} {device.model}
											</strong>
											<div class="flex items-center gap-1.5 mt-0.5">
												<span class="rounded bg-ash px-1.5 py-0.5 text-[10.5px] font-semibold text-secondary border border-stone/70">
													{device.deviceType}
												</span>
												{#if device.identifierLabel}
													<span class="rounded bg-white px-1.5 py-0.5 text-[10.5px] font-medium text-secondary border border-stone/80">
														{device.identifierLabel}
													</span>
												{/if}
											</div>
										</div>
									</div>

									<div class="flex items-center gap-1.5">
										{#if canWriteDevice}
											<button
												type="button"
												onclick={() => openEditDevice(device)}
												class="inline-flex h-7 items-center gap-1 rounded-button border border-stone bg-white px-2.5 text-xs font-semibold text-ink hover:bg-ash transition-colors shadow-2xs cursor-pointer"
											>
												<span class="material-symbols-outlined text-[14px]">edit</span>
												<span>{t('customers.detail.edit')}</span>
											</button>
										{/if}
										{#if canDeleteDevice}
											<form method="POST" action="?/deleteDevice" use:enhance>
												<input type="hidden" name="deviceId" value={device.id} />
												<DeleteConfirmButton
													label={t('common.delete')}
													confirmTitle={t('common.deleteConfirmTitle')}
													confirmMessage={t('common.deleteConfirm')}
													cancelLabel={t('common.cancel')}
													class="inline-flex h-7 items-center justify-center rounded-button border border-error/30 bg-error/10 px-2 text-xs font-semibold text-error hover:bg-error/20 transition-colors shadow-2xs cursor-pointer"
												/>
											</form>
										{/if}
									</div>
								</div>

								<div class="grid grid-cols-1 sm:grid-cols-2 gap-2 text-xs pt-1">
									<div class="flex items-center gap-2">
										<span class="text-secondary font-label-caps uppercase text-[10.5px] font-bold">{t('customers.detail.serial')}:</span>
										<span class="text-ink font-mono">{device.serialNumber || '—'}</span>
									</div>
									<div class="flex items-center gap-2">
										<span class="text-secondary font-label-caps uppercase text-[10.5px] font-bold">{t('customers.detail.notes')}:</span>
										<span class="text-secondary truncate">{device.notes || '—'}</span>
									</div>
								</div>

								<!-- Expandable Device Repair History -->
								<details class="border-t border-stone/60 pt-2.5 group">
									<summary class="cursor-pointer text-xs font-semibold text-ink flex items-center justify-between select-none">
										<span>{t('customers.devices.history')} ({historyForDevice(device.id).length})</span>
										<span class="material-symbols-outlined text-[18px] text-secondary transition-transform group-open:rotate-180">
											expand_more
										</span>
									</summary>

									{#if historyForDevice(device.id).length === 0}
										<p class="text-secondary text-xs mt-2 mb-0 italic">
											{t('customers.devices.historyEmpty')}
										</p>
									{:else}
										<div class="space-y-2 mt-2.5">
											{#each historyForDevice(device.id) as ticket (ticket.id)}
												<a
													href={`/app/tickets/${ticket.id}`}
													class="block rounded-lg border border-stone/80 bg-surface-container-low/40 p-2.5 hover:bg-ash/50 transition-colors"
												>
													<div class="flex items-center justify-between gap-md">
														<div>
															<strong class="text-ink text-xs font-mono font-bold block">{ticket.ticketNumber}</strong>
															<p class="text-[11px] text-secondary mt-0.5 mb-0">
																{formatDate(ticket.createdAt, data.locale, { dateStyle: 'medium' })}
															</p>
														</div>
														<span class="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10.5px] font-bold uppercase tracking-wider border {getStatusPillClasses(ticket.status)}">
															<span class="w-1.5 h-1.5 rounded-full" style="background-color: {ticket.statusColor || '#64748b'};"></span>
															<span>{ticket.status}</span>
														</span>
													</div>
													{#if ticket.warrantyActive}
														<span class="inline-block mt-1 text-emerald-700 text-[10.5px] font-bold">
															★ {t('customers.history.warranty')}
														</span>
													{/if}
												</a>
											{/each}
										</div>
									{/if}
								</details>
							</div>
						{/each}
					</div>
				{/if}
			</section>

			<!-- Service History Timeline / List -->
			<section data-bento class="bg-white border border-stone rounded-2xl p-6 shadow-2xs space-y-4">
				<div class="flex items-center justify-between border-b border-stone/60 pb-3">
					<div class="flex items-center gap-2">
						<span class="material-symbols-outlined text-secondary text-[20px]">history</span>
						<h3 class="font-subheading text-[15px] font-bold text-ink">{t('customers.history.title')}</h3>
					</div>
					<span class="rounded-full bg-ash px-2.5 py-0.5 text-[11px] font-bold text-secondary">
						{serviceHistory.length}
					</span>
				</div>

				{#if serviceHistory.length === 0}
					<p class="text-body-sm text-secondary m-0 py-4 text-center">{t('customers.history.empty')}</p>
				{:else}
					<div class="space-y-2.5">
						{#each serviceHistory as ticket (ticket.id)}
							<a
								href={`/app/tickets/${ticket.id}`}
								class="block rounded-xl border border-stone bg-white p-3.5 hover:bg-ash/40 transition-colors shadow-2xs"
							>
								<div class="flex items-start justify-between gap-md">
									<div>
										<strong class="text-ink font-mono font-bold text-body-sm">{ticket.ticketNumber}</strong>
										<p class="text-xs text-secondary mt-0.5 mb-0">{ticket.device}</p>
									</div>
									<span class="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-[11px] font-bold uppercase tracking-wider border {getStatusPillClasses(ticket.status)}">
										<span class="w-1.5 h-1.5 rounded-full" style="background-color: {ticket.statusColor || '#64748b'};"></span>
										<span>{ticket.status}</span>
									</span>
								</div>
								<div class="mt-2 flex items-center justify-between text-xs border-t border-stone/50 pt-2 text-secondary">
									<span>{formatDate(ticket.createdAt, data.locale, { dateStyle: 'medium' })}</span>
									{#if ticket.warrantyActive}
										<span class="text-emerald-700 font-bold">★ {t('customers.history.warranty')}</span>
									{/if}
								</div>
							</a>
						{/each}
					</div>
				{/if}
			</section>
		</div>

		<!-- Right Sidebar Column (4 cols) -->
		<aside class="lg:col-span-4 space-y-md">
			<!-- Operational Status & Flags -->
			<section data-bento class="bg-white border border-stone rounded-2xl p-5 shadow-2xs space-y-4">
				<h3 class="font-subheading text-[15px] font-bold text-ink border-b border-stone/60 pb-3">
					{t('customers.detail.operationalStatus')}
				</h3>

				<!-- Star Status Toggle -->
				<div class="flex items-center justify-between gap-md border-b border-stone/60 pb-3">
					<div>
						<span class="block text-body-sm font-semibold text-ink">{t('customers.detail.starStatus')}</span>
						<span class="text-xs text-secondary">{t('customers.detail.starStatusHint')}</span>
					</div>
					{#if canWriteCustomer}
						<form method="POST" action={customer.isStarred ? '?/unstar' : '?/star'} use:enhance>
							<button
								type="submit"
								class="inline-flex h-8 items-center gap-1 rounded-button border border-stone bg-white px-2.5 text-xs font-semibold text-ink hover:bg-ash transition-colors shadow-2xs cursor-pointer"
							>
								<span class="material-symbols-outlined text-[15px] {customer.isStarred ? 'fill-icon text-amber-500' : 'text-secondary'}">
									star
								</span>
								<span>{customer.isStarred ? t('customers.detail.unstar') : t('customers.detail.star')}</span>
							</button>
						</form>
					{/if}
				</div>

				<!-- Ban Status Toggle -->
				<div class="space-y-3">
					<div class="flex items-center justify-between gap-md">
						<div>
							<span class="block text-body-sm font-semibold text-ink">{t('customers.detail.banStatus')}</span>
							<span class="text-xs text-secondary">{t('customers.detail.banStatusHint')}</span>
						</div>
						{#if canWriteCustomer}
							{#if customer.isBanned}
								<form method="POST" action="?/unban" use:enhance>
									<button
										type="submit"
										class="inline-flex h-8 items-center rounded-button border border-stone bg-white px-2.5 text-xs font-semibold text-ink hover:bg-ash transition-colors shadow-2xs cursor-pointer"
									>
										{t('customers.detail.unban')}
									</button>
								</form>
							{:else}
								<button
									type="button"
									onclick={() => (showBanModal = true)}
									class="inline-flex h-8 items-center rounded-button border border-error/30 bg-error/10 px-2.5 text-xs font-semibold text-error hover:bg-error/20 transition-colors shadow-2xs cursor-pointer"
								>
									{t('customers.detail.ban')}
								</button>
							{/if}
						{/if}
					</div>

					{#if customer.isBanned}
						<div class="rounded-xl border border-rose-300 bg-rose-500/10 p-3 text-xs text-rose-800 space-y-1">
							<strong class="block uppercase tracking-wider font-label-caps">{t('customers.list.status.banned')}</strong>
							{#if customer.banReason}
								<p class="m-0 leading-relaxed font-mono">{customer.banReason}</p>
							{/if}
						</div>
					{/if}
				</div>
			</section>

			<!-- Customer Metadata Card -->
			<section data-bento class="bg-surface-container-low/40 border border-stone rounded-2xl p-5 shadow-2xs space-y-2.5 text-xs">
				<h4 class="font-bold uppercase tracking-wider text-secondary font-label-caps mb-1">
					Audit Records
				</h4>
				<div class="flex items-center justify-between py-1 border-b border-stone/50">
					<span class="text-secondary">{t('customers.detail.customerId')}</span>
					<span class="font-mono text-ink font-bold">C-{customer.id}</span>
				</div>
				<div class="flex items-center justify-between py-1 border-b border-stone/50">
					<span class="text-secondary">{t('customers.detail.registered')}</span>
					<span class="text-ink font-mono">{formatDate(customer.createdAt, data.locale, { dateStyle: 'medium', timeStyle: 'short' })}</span>
				</div>
				{#if customer.updatedAt}
					<div class="flex items-center justify-between py-1">
						<span class="text-secondary">{t('customers.detail.lastUpdated')}</span>
						<span class="text-ink font-mono">{formatDate(customer.updatedAt, data.locale, { dateStyle: 'medium', timeStyle: 'short' })}</span>
					</div>
				{/if}
			</section>
		</aside>
	</div>
</div>

<!-- Add / Edit Device Modal -->
{#if showDeviceModal && canWriteDevice}
	<div transition:fade={{ duration: 150 }} class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-xs p-4" role="presentation">
		<div class="w-[min(620px,calc(100vw-2rem))] max-h-[calc(100vh-3rem)] overflow-y-auto rounded-2xl border border-stone bg-white p-6 shadow-2xl space-y-5" role="dialog" aria-modal="true">
			<div class="flex items-start justify-between gap-md border-b border-stone pb-3.5">
				<div>
					<h3 class="font-subheading text-[16px] font-bold text-ink">
						{editingDevice ? t('customers.detail.edit') : t('customers.detail.addDevice')}
					</h3>
					<p class="text-xs text-secondary mt-0.5">
						{customer.fullName} · ID: C-{customer.id}
					</p>
				</div>
				<button type="button" onclick={closeDeviceModal} class="text-secondary hover:text-ink cursor-pointer p-1">
					<span class="material-symbols-outlined text-[20px]">close</span>
				</button>
			</div>

			<form
				method="POST"
				action={editingDevice ? '?/updateDevice' : '?/createDevice'}
				class="space-y-4"
				use:enhance={() => {
					return async ({ result, update }) => {
						await update();
						if (result.type === 'success') {
							closeDeviceModal();
						}
					};
				}}
			>
				{#if editingDevice}
					<input type="hidden" name="deviceId" value={editingDevice.id} />
				{/if}

				<div class="grid grid-cols-1 sm:grid-cols-3 gap-sm">
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">
							{t('customers.detail.deviceType')} *
						</span>
						<input
							name="deviceType"
							value={editingDevice?.deviceType ?? ''}
							placeholder={t('customers.detail.deviceTypePlaceholder')}
							required
							class="w-full rounded-input border border-stone bg-white p-2 text-body-sm text-ink focus:border-ink focus:outline-none"
						/>
					</label>
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">
							{t('customers.detail.brand')} *
						</span>
						<input
							name="brand"
							value={editingDevice?.brand ?? ''}
							placeholder={t('customers.detail.brandPlaceholder')}
							required
							class="w-full rounded-input border border-stone bg-white p-2 text-body-sm text-ink focus:border-ink focus:outline-none"
						/>
					</label>
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">
							{t('customers.detail.model')} *
						</span>
						<input
							name="model"
							value={editingDevice?.model ?? ''}
							placeholder={t('customers.detail.modelPlaceholder')}
							required
							class="w-full rounded-input border border-stone bg-white p-2 text-body-sm text-ink focus:border-ink focus:outline-none"
						/>
					</label>
				</div>

				<div class="grid grid-cols-1 sm:grid-cols-2 gap-sm">
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">
							{t('customers.detail.serialNumber')}
						</span>
						<input
							name="serialNumber"
							value={editingDevice?.serialNumber ?? ''}
							placeholder={t('customers.detail.optional')}
							class="w-full rounded-input border border-stone bg-white p-2 text-body-sm text-ink font-mono focus:border-ink focus:outline-none"
						/>
					</label>
					<label class="space-y-1 block">
						<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">
							{t('customers.detail.identifierLabel')}
						</span>
						<input
							name="identifierLabel"
							value={editingDevice?.identifierLabel ?? ''}
							placeholder={t('customers.detail.identifierPlaceholder')}
							class="w-full rounded-input border border-stone bg-white p-2 text-body-sm text-ink focus:border-ink focus:outline-none"
						/>
					</label>
				</div>

				<label class="space-y-1 block">
					<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">
						{t('customers.detail.notes')}
					</span>
					<textarea
						name="notes"
						rows="3"
						placeholder={t('customers.detail.deviceNotesPlaceholder')}
						class="w-full rounded-input border border-stone bg-white p-2.5 text-body-sm text-ink focus:border-ink focus:outline-none resize-none leading-relaxed"
					>{editingDevice?.notes ?? ''}</textarea>
				</label>

				<div class="flex justify-end gap-2 border-t border-stone pt-3.5">
					<button
						type="button"
						onclick={closeDeviceModal}
						class="px-4 py-2 rounded-button border border-stone bg-white text-ink hover:bg-ash text-body-sm font-medium transition-all shadow-2xs cursor-pointer"
					>
						{t('common.cancel')}
					</button>
					<button
						type="submit"
						class="px-4 py-2 rounded-button bg-ink text-white font-medium text-body-sm hover:bg-ink/90 active:scale-95 transition-all shadow-xs cursor-pointer"
					>
						{editingDevice ? t('customers.detail.saveDevice') : t('customers.detail.createDevice')}
					</button>
				</div>
			</form>
		</div>
	</div>
{/if}

<!-- Ban Customer Modal -->
{#if showBanModal && !customer.isBanned && canWriteCustomer}
	<div transition:fade={{ duration: 150 }} class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-xs p-4" role="presentation">
		<div class="w-[min(480px,calc(100vw-2rem))] rounded-2xl border border-stone bg-white p-6 shadow-2xl space-y-4" role="dialog" aria-modal="true">
			<div class="flex items-start justify-between gap-md border-b border-stone pb-3">
				<div>
					<h3 class="font-subheading text-[16px] font-bold text-rose-700">{t('customers.detail.banStatus')}</h3>
					<p class="text-xs text-secondary mt-0.5">{t('customers.detail.banStatusHint')}</p>
				</div>
				<button type="button" onclick={() => (showBanModal = false)} class="text-secondary hover:text-ink cursor-pointer p-1">
					<span class="material-symbols-outlined text-[20px]">close</span>
				</button>
			</div>

			<form
				method="POST"
				action="?/ban"
				class="space-y-4"
				use:enhance={() => {
					return async ({ result, update }) => {
						await update();
						if (result.type === 'success') {
							showBanModal = false;
						}
					};
				}}
			>
				<label class="space-y-1 block">
					<span class="text-[11px] font-bold uppercase tracking-wider text-secondary font-label-caps block">
						{t('customers.detail.banReason')} *
					</span>
					<textarea
						name="reason"
						required
						rows="3"
						placeholder={t('customers.detail.banReasonPlaceholder')}
						class="w-full rounded-input border border-stone bg-white p-2.5 text-body-sm text-ink focus:border-ink focus:outline-none resize-none"
					></textarea>
				</label>

				<div class="flex justify-end gap-2 border-t border-stone pt-3">
					<button
						type="button"
						onclick={() => (showBanModal = false)}
						class="px-4 py-2 rounded-button border border-stone bg-white text-ink hover:bg-ash text-body-sm font-medium transition-all shadow-2xs cursor-pointer"
					>
						{t('common.cancel')}
					</button>
					<button
						type="submit"
						class="px-4 py-2 rounded-button bg-rose-600 text-white font-medium text-body-sm hover:bg-rose-700 active:scale-95 transition-all shadow-xs cursor-pointer"
					>
						{t('customers.detail.confirmBan')}
					</button>
				</div>
			</form>
		</div>
	</div>
{/if}
