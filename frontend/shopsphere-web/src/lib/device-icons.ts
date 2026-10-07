export const deviceIcons = {
  laptop: "/products/laptop.svg",
  headphones: "/products/headphones.svg",
  mouse: "/products/mouse.svg",
  monitor: "/products/monitor.svg",
  keyboard: "/products/keyboard.svg",
  ssd: "/products/ssd.svg",
} as const;

const categoryIcons: Record<string, string> = {
  Laptops: deviceIcons.laptop,
  Audio: deviceIcons.headphones,
  Monitors: deviceIcons.monitor,
  Storage: deviceIcons.ssd,
};

export function resolveDeviceIcon(src: string, category?: string) {
  const kind = src.split("?")[0].split("/").pop()?.split(".")[0];
  if (kind && Object.hasOwn(deviceIcons, kind)) {
    return deviceIcons[kind as keyof typeof deviceIcons];
  }
  return category ? categoryIcons[category] : undefined;
}
