/**
 * Svelte custom action to animate text reveal word-by-word on mount.
 * ponytail: Native platform implementation, dynamic keyframe injection, zero dependencies.
 */
if (typeof document !== 'undefined') {
	const styleId = 'svelte-text-reveal-keyframes';
	if (!document.getElementById(styleId)) {
		const style = document.createElement('style');
		style.id = styleId;
		style.textContent = `
			@keyframes textRevealFadeIn {
				from {
					opacity: 0;
					transform: translate3d(0, 10px, 0);
				}
				to {
					opacity: 1;
					transform: translate3d(0, 0, 0);
				}
			}
		`;
		document.head.appendChild(style);
	}
}

export function textReveal(
	node: HTMLElement,
	{ stagger = 0.04, duration = 0.5, startDelay = 0 }: { stagger?: number; duration?: number; startDelay?: number } = {}
) {
	const text = node.textContent || '';
	const words = text.trim().split(/\s+/);
	node.innerHTML = '';

	words.forEach((word, index) => {
		const span = document.createElement('span');
		// Add space after word
		span.innerText = word + '\u00A0';
		span.style.display = 'inline-block';
		span.style.opacity = '0';
		span.style.transform = 'translate3d(0, 10px, 0)';
		span.style.animation = `textRevealFadeIn ${duration}s cubic-bezier(0.215, 0.61, 0.355, 1) forwards`;
		span.style.animationDelay = `${startDelay + index * stagger}s`;
		node.appendChild(span);
	});

	return {
		destroy() {
			node.innerHTML = text;
		}
	};
}
