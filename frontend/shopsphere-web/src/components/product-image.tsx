import Image from "next/image";
import { resolveDeviceIcon } from "@/lib/device-icons";
import { Icon } from "./icon";
export function ProductImage({
  src,
  name,
  large = false,
  category,
}: {
  src: string;
  name: string;
  large?: boolean;
  category?: string;
}) {
  const uploaded = src?.startsWith("/api/media/");
  const icon = uploaded ? src : resolveDeviceIcon(src, category);
  if (!icon)
    return (
      <div
        className="product-image device-placeholder"
        role="img"
        aria-label={name}
      >
        <Icon name="box" size={large ? 120 : 80} />
      </div>
    );
  return (
    <Image
      src={icon}
      alt={name}
      width={large ? 640 : 400}
      height={large ? 480 : 300}
      className="product-image"
      priority={large}
      unoptimized={uploaded}
    />
  );
}
