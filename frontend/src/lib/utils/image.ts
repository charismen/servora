const SUPPORTED_IMAGE_MIME_TYPES = new Set(['image/jpeg', 'image/png', 'image/webp']);

export function resolveSupportedImageMime(file: Pick<File, 'type' | 'name'>): string | null {
	const rawType = file.type.split(';')[0].trim().toLowerCase();
	const normalized = rawType === 'image/jpg' ? 'image/jpeg' : rawType;
	if (SUPPORTED_IMAGE_MIME_TYPES.has(normalized)) {
		return normalized;
	}
	if (!normalized || normalized === 'application/octet-stream') {
		const lowerName = file.name.toLowerCase();
		if (/\.(jpe?g)$/i.test(lowerName)) return 'image/jpeg';
		if (/\.png$/i.test(lowerName)) return 'image/png';
		if (/\.webp$/i.test(lowerName)) return 'image/webp';
	}
	return null;
}

export function isSupportedImageFile(file: Pick<File, 'type' | 'name'>): boolean {
	return resolveSupportedImageMime(file) !== null;
}

export function normalizeImageFile(file: File): File {
	const mime = resolveSupportedImageMime(file);
	if (!mime || file.type.toLowerCase() === mime) return file;
	return new File([file], file.name, {
		type: mime,
		lastModified: file.lastModified
	});
}
