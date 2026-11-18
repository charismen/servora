import { env } from '$env/dynamic/public';

export function apiAssetUrl(path?: string | null) {
	if (!path) return '';
	if (/^https?:\/\//i.test(path)) return path;
	const apiBaseUrl = env.PUBLIC_API_BASE_URL ?? 'http://localhost:5051/api/v1';
	return `${apiBaseUrl.replace(/\/api\/v\d+\/?$/, '')}${path}`;
}

/**
 * Compresses an image file in-browser using HTML5 Canvas before uploading.
 * Reduces 5MB-15MB smartphone photos down to ~300KB-600KB with crisp diagnostic detail.
 */
export async function compressImage(
	file: File,
	maxDimension = 1920,
	quality = 0.82
): Promise<File> {
	if (typeof window === 'undefined') return file;
	if (!file.type.startsWith('image/') || file.type === 'image/svg+xml') {
		return file;
	}

	return new Promise((resolve) => {
		const img = new Image();
		const objectUrl = URL.createObjectURL(file);

		img.onload = () => {
			URL.revokeObjectURL(objectUrl);
			let { width, height } = img;

			// If already small enough in dimension and under 800KB, keep original
			if (width <= maxDimension && height <= maxDimension && file.size <= 800 * 1024) {
				return resolve(file);
			}

			if (width > height) {
				if (width > maxDimension) {
					height = Math.round((height * maxDimension) / width);
					width = maxDimension;
				}
			} else {
				if (height > maxDimension) {
					width = Math.round((width * maxDimension) / height);
					height = maxDimension;
				}
			}

			const canvas = document.createElement('canvas');
			canvas.width = width;
			canvas.height = height;
			const ctx = canvas.getContext('2d');
			if (!ctx) return resolve(file);

			ctx.drawImage(img, 0, 0, width, height);

			const outputType = file.type === 'image/png' ? 'image/png' : 'image/jpeg';
			canvas.toBlob(
				(blob) => {
					if (!blob || blob.size >= file.size) {
						return resolve(file);
					}
					const targetName = file.name.replace(/\.[^.]+$/, outputType === 'image/jpeg' ? '.jpg' : '.png');
					const compressedFile = new File([blob], targetName, {
						type: outputType,
						lastModified: Date.now()
					});
					resolve(compressedFile);
				},
				outputType,
				quality
			);
		};

		img.onerror = () => {
			URL.revokeObjectURL(objectUrl);
			resolve(file);
		};

		img.src = objectUrl;
	});
}

