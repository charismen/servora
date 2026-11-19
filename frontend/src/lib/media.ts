import { env } from '$env/dynamic/public';
import { isSupportedImageFile, normalizeImageFile, resolveSupportedImageMime } from '$lib/utils/image';

export { isSupportedImageFile, normalizeImageFile, resolveSupportedImageMime };

const DEFAULT_MAX_COMPRESSED_BYTES = 700 * 1024;

export function apiAssetUrl(path?: string | null) {
	if (!path) return '';
	if (/^https?:\/\//i.test(path)) return path;
	const apiBaseUrl = env.PUBLIC_API_BASE_URL ?? 'http://localhost:5051/api/v1';
	return `${apiBaseUrl.replace(/\/api\/v\d+\/?$/, '')}${path}`;
}

function replaceImageExtension(fileName: string, ext: string): string {
	return /\.[^.]+$/.test(fileName) ? fileName.replace(/\.[^.]+$/, ext) : `${fileName}${ext}`;
}

function fitDimensions(width: number, height: number, maxDimension: number) {
	if (width <= maxDimension && height <= maxDimension) {
		return { width, height };
	}
	if (width > height) {
		return {
			width: maxDimension,
			height: Math.max(1, Math.round((height * maxDimension) / width))
		};
	}
	return {
		width: Math.max(1, Math.round((width * maxDimension) / height)),
		height: maxDimension
	};
}

function canvasToBlob(
	img: HTMLImageElement,
	width: number,
	height: number,
	outputType: 'image/jpeg' | 'image/png',
	quality: number
): Promise<Blob | null> {
	const canvas = document.createElement('canvas');
	canvas.width = width;
	canvas.height = height;
	const ctx = canvas.getContext('2d');
	if (!ctx) return Promise.resolve(null);

	if (outputType === 'image/jpeg') {
		ctx.fillStyle = '#ffffff';
		ctx.fillRect(0, 0, width, height);
	}
	ctx.drawImage(img, 0, 0, width, height);

	return new Promise((resolve) => {
		canvas.toBlob((blob) => resolve(blob), outputType, quality);
	});
}

/**
 * Compresses an image file in-browser using HTML5 Canvas before uploading.
 * Targets <= 700KB per photo so single-file uploads stay comfortably below proxy upload limits.
 */
export async function compressImage(
	file: File,
	maxDimension = 1600,
	quality = 0.82,
	options: { maxBytes?: number; preservePng?: boolean } = {}
): Promise<File> {
	const normalized = normalizeImageFile(file);
	if (typeof window === 'undefined') return normalized;
	if (!normalized.type.startsWith('image/') || normalized.type === 'image/svg+xml') {
		return normalized;
	}

	const maxBytes = options.maxBytes ?? DEFAULT_MAX_COMPRESSED_BYTES;
	const preservePng = options.preservePng ?? false;

	return new Promise((resolve) => {
		const img = new Image();
		const objectUrl = URL.createObjectURL(normalized);

		img.onload = async () => {
			URL.revokeObjectURL(objectUrl);
			try {
				if (img.width <= maxDimension && img.height <= maxDimension && normalized.size <= maxBytes) {
					resolve(normalized);
					return;
				}

				const base = fitDimensions(img.width, img.height, maxDimension);
				let bestBlob: Blob | null = null;
				let bestType: 'image/jpeg' | 'image/png' = 'image/jpeg';

				if (preservePng && normalized.type === 'image/png') {
					for (const scale of [1, 0.75, 0.5]) {
						const w = Math.max(1, Math.round(base.width * scale));
						const h = Math.max(1, Math.round(base.height * scale));
						const blob = await canvasToBlob(img, w, h, 'image/png', 1);
						if (!blob) continue;
						if (!bestBlob || blob.size < bestBlob.size) {
							bestBlob = blob;
							bestType = 'image/png';
						}
						if (blob.size <= maxBytes) break;
					}
				} else {
					const qualities = Array.from(new Set([quality, 0.72, 0.62, 0.5])).sort((a, b) => b - a);
					for (const q of qualities) {
						const blob = await canvasToBlob(img, base.width, base.height, 'image/jpeg', q);
						if (!blob) continue;
						if (!bestBlob || blob.size < bestBlob.size) {
							bestBlob = blob;
							bestType = 'image/jpeg';
						}
						if (blob.size <= maxBytes) break;
					}
					if (!bestBlob || bestBlob.size > maxBytes) {
						const scaledW = Math.max(1, Math.round(base.width * 0.75));
						const scaledH = Math.max(1, Math.round(base.height * 0.75));
						for (const q of [0.7, 0.55]) {
							const blob = await canvasToBlob(img, scaledW, scaledH, 'image/jpeg', q);
							if (!blob) continue;
							if (!bestBlob || blob.size < bestBlob.size) {
								bestBlob = blob;
								bestType = 'image/jpeg';
							}
							if (blob.size <= maxBytes) break;
						}
					}
				}

				if (!bestBlob || (bestBlob.size >= normalized.size && normalized.size <= maxBytes)) {
					resolve(normalized);
					return;
				}

				const targetName = replaceImageExtension(normalized.name, bestType === 'image/jpeg' ? '.jpg' : '.png');
				resolve(
					new File([bestBlob], targetName, {
						type: bestType,
						lastModified: Date.now()
					})
				);
			} catch {
				resolve(normalized);
			}
		};

		img.onerror = () => {
			URL.revokeObjectURL(objectUrl);
			resolve(normalized);
		};

		img.src = objectUrl;
	});
}
