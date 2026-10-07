import Image from "next/image";
export function ProductImage({ src, name, large = false }: { src: string; name: string; large?: boolean }) {
  return <Image src={src} alt={name} width={large ? 640 : 400} height={large ? 480 : 300} className="product-image" priority={large} />;
}
