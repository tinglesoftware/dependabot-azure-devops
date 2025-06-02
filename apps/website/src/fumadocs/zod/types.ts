export type GitFileInfo = {
  /** Relevant commit date in ISO format. */
  date: string;

  /** Timestamp in **seconds**, as returned from git. */
  timestamp: number;

  /** The author's name, as returned from git. */
  author: string;
};

export const ImageFormats = ['png', 'jpg', 'jpeg', 'webp', 'avif', 'tiff', 'gif', 'svg'] as const;
export type ImageFormat = (typeof ImageFormats)[number];

export type ImageData = {
  src: string;
  /**
   * Alt text for the image.
   *
   * Can be provided via ...
   *   - JSON: `"myImageField": { "alt": "My alt text", "src": "my-image.jpg" }`
   *   - YAML / Frontmatter:
   *     ```yaml
   *     # ...
   *     myImageField:
   *       alt: My alt text
   *       src: my-image.jpg
   *     ```
   */
  alt?: string;
  format: ImageFormat;
  height: number;
  width: number;
  blurDataURL?: string;
  blurWidth?: number;
  blurHeight?: number;
  /** `width` / `height` (see https://en.wikipedia.org/wiki/Aspect_ratio_(image)) */
  aspectRatio: number;
};
