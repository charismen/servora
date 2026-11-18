export type ToastVariant = 'success' | 'error' | 'warning' | 'info';

export interface ToastOptions {
	id?: string;
	title?: string;
	duration?: number;
	dismissible?: boolean;
}

export interface ToastItem {
	id: string;
	message: string;
	title?: string;
	variant: ToastVariant;
	duration: number;
	dismissible: boolean;
	timer?: ReturnType<typeof setTimeout>;
}

class ToastStore {
	toasts = $state<ToastItem[]>([]);

	show(message: string, variant: ToastVariant = 'info', options: ToastOptions = {}): string | undefined {
		if (!message || typeof message !== 'string') return;
		const id = options.id || `toast-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
		const duration = options.duration ?? (variant === 'error' ? 6000 : 4000);
		const dismissible = options.dismissible ?? true;

		// Deduplicate if identical message is already showing (update in place to avoid transition overlap)
		const existingIndex = this.toasts.findIndex((t) => t.message === message);
		if (existingIndex !== -1) {
			const existing = this.toasts[existingIndex];
			if (existing.timer) clearTimeout(existing.timer);
			let refreshedTimer: ReturnType<typeof setTimeout> | undefined;
			if (duration > 0) {
				refreshedTimer = setTimeout(() => {
					this.dismiss(existing.id);
				}, duration);
			}
			this.toasts[existingIndex] = {
				...existing,
				title: options.title ?? existing.title,
				variant,
				duration,
				dismissible,
				timer: refreshedTimer
			};
			return existing.id;
		}

		let timer: ReturnType<typeof setTimeout> | undefined;
		if (duration > 0) {
			timer = setTimeout(() => {
				this.dismiss(id);
			}, duration);
		}

		const item: ToastItem = {
			id,
			message,
			title: options.title,
			variant,
			duration,
			dismissible,
			timer
		};

		// Limit maximum visible toasts to 3 to prevent screen flooding
		if (this.toasts.length >= 3) {
			const oldest = this.toasts[0];
			if (oldest.timer) clearTimeout(oldest.timer);
			this.toasts.shift();
		}

		this.toasts.push(item);
		return id;
	}

	success(message: string, options?: ToastOptions): string | undefined {
		return this.show(message, 'success', options);
	}

	error(message: string, options?: ToastOptions): string | undefined {
		return this.show(message, 'error', options);
	}

	warning(message: string, options?: ToastOptions): string | undefined {
		return this.show(message, 'warning', options);
	}

	info(message: string, options?: ToastOptions): string | undefined {
		return this.show(message, 'info', options);
	}

	dismiss(id: string): void {
		const index = this.toasts.findIndex((t) => t.id === id);
		if (index !== -1) {
			const [toastItem] = this.toasts.splice(index, 1);
			if (toastItem.timer) clearTimeout(toastItem.timer);
		}
	}

	clear(): void {
		for (const t of this.toasts) {
			if (t.timer) clearTimeout(t.timer);
		}
		this.toasts = [];
	}
}

export const toast = new ToastStore();
