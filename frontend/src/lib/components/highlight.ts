/**
 * Svelte custom action to make an element have a hover spotlight highlight effect.
 * ponytail: Native platform CSS/DOM implementation, zero dependencies.
 */
export function highlight(node: HTMLElement, options = { color: 'rgba(85, 95, 109, 0.08)', size: 100 }) {
	// Create spotlight layer (rendered underneath child content)
	const glow = document.createElement('span');
	glow.style.position = 'absolute';
	glow.style.inset = '0';
	glow.style.pointerEvents = 'none';
	glow.style.borderRadius = 'inherit';
	glow.style.opacity = '0';
	glow.style.transition = 'opacity 0.25s ease';
	glow.style.zIndex = '0';

	node.appendChild(glow);

	const handleMouseMove = (e: MouseEvent) => {
		const rect = node.getBoundingClientRect();
		const x = e.clientX - rect.left;
		const y = e.clientY - rect.top;
		glow.style.background = `radial-gradient(${options.size}px circle at ${x}px ${y}px, ${options.color}, transparent)`;
		glow.style.opacity = '1';
	};

	const handleMouseLeave = () => {
		glow.style.opacity = '0';
	};

	node.addEventListener('mousemove', handleMouseMove);
	node.addEventListener('mouseleave', handleMouseLeave);

	// Ensure the parent is positioned correctly
	if (getComputedStyle(node).position === 'static') {
		node.style.position = 'relative';
	}

	// Elevate sibling children z-indexes so text/icons sit above the glow layer
	const children = Array.from(node.children) as HTMLElement[];
	children.forEach((child) => {
		if (child !== glow && getComputedStyle(child).zIndex === 'auto') {
			child.style.position = 'relative';
			child.style.zIndex = '1';
		}
	});

	return {
		destroy() {
			node.removeEventListener('mousemove', handleMouseMove);
			node.removeEventListener('mouseleave', handleMouseLeave);
			glow.remove();
		}
	};
}
