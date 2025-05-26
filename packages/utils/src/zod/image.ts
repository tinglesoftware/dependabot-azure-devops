import { createHash } from 'node:crypto';
import { existsSync } from 'node:fs';
import { copyFile, mkdir, readFile } from 'node:fs/promises';
import { basename, extname, join, relative, resolve } from 'node:path';
import sharp, { type FormatEnum } from 'sharp';
import { z } from 'zod';

import { ImageFormats, type ImageData, type ImageFormat } from './types.js';

export type ImageParams = {
  /**
   * Path of the current document.
   * Images will be resolved relative to this path or its directory.
   */
  path: string;

  /**
   * Whether to allow remote URLs such as `https://example.com/image.png`.
   *
   * @default false
   */
  remote?: boolean;

  /**
   * Whether to emit non-remote images to the output.
   * This can be disabled if you only need the metadata such as when the image
   * is already in the output directory.
   *
   * @default true
   */
  emit?: boolean;

  /**
   * The directory of the assets (relative to current working), where to write the output assets.
   * @default 'public/static'
   */
  output?: string;

  /**
   * The public base path of the assets
   * @default '/static/'
   * @example
   * '/' -> '/image.png'
   * '/static/' -> '/static/image.png'
   * './static/' -> './static/image.png'
   */
  baseUrl?: '/' | `/${string}/` | `.${string}/`;

  /**
   * This option determines the name format of each output asset.
   * The asset will be written to the directory specified in the `output.assets` option.
   * You can use `[name]`, `[hash]` and `[ext]` template strings with specify length.
   * @default '[name]-[hash:8].[ext]'
   */
  format?: string;
};

export function image({
  path,
  remote = false,
  emit = true,
  output = 'public/static',
  baseUrl = '/static/',
  format = '[name]-[hash:8].[ext]',
}: ImageParams) {
  return z
    .union([z.string(), z.object({ src: z.string(), alt: z.string().optional() })])
    .transform<ImageData>(async (value, { addIssue }) => {
      const { src, alt } = typeof value === 'string' ? { src: value, alt: undefined } : value;
      try {
        // checks if the string starts with http:// or https://
        if (/^https?:\/\//.test(src)) {
          // ensure remote URLs are allowed
          if (!remote) throw new Error('Remote images must be explicitly allowed');

          const response = await fetch(src);
          if (!response.ok) throw new Error(`Failed to fetch image at ${src}`);
          const buffer = await (await response.blob()).arrayBuffer();
          const metadata = await getImageMetadata(Buffer.from(buffer));
          if (!metadata) throw new Error(`Failed to extract image metadata from ${src}`);
          return { src, alt, ...metadata };
        }

        // at this point, it is not a remote URL

        // ensure the file exists
        const resolvedFilePath = resolve(path, '..', src);
        if (!existsSync(resolvedFilePath)) throw new Error(`Image ${src} does not exist. Is the path correct?`);

        // if not emitting, we only need to get the metadata
        if (!emit) {
          const buffer = await readFile(resolvedFilePath);
          const metadata = await getImageMetadata(buffer);
          if (!metadata) throw new Error(`Failed to extract image metadata from ${resolvedFilePath}`);
          return { src, alt, ...metadata };
        }

        const destination = relative(process.cwd(), output);

        return {
          ...(await processAsset(
            {
              input: src,
              from: path,
              format,
              baseUrl,
              destination,
            },
            true,
          )),
          alt,
        };
      } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        addIssue({ fatal: true, code: 'custom', message });
        return null as never;
      }
    });
}

const emitted: Record<string, string> = {};

/**
 * Processes a referenced asset of a file.
 * @param input - The relative path of the asset.
 * @param from - The source file path.
 * @param format - The output filename template.
 * @param baseUrl - The output public base URL.
 * @param isImage - If true, processes the asset as an image and returns an image object with blurDataURL.
 * @returns The reference public URL or an image object.
 */
export async function processAsset<T extends true | undefined = undefined>(
  {
    input,
    from,
    format,
    baseUrl,
    destination,
  }: {
    input: string;
    from: string;
    format: string;
    baseUrl: string;
    destination: string;
  },
  isImage?: T,
): Promise<T extends true ? ImageData : string> {
  // e.g. input = '../assets/image.png?foo=bar#hash'
  const queryIdx = input.indexOf('?');
  const hashIdx = input.indexOf('#');
  const index = Math.min(queryIdx >= 0 ? queryIdx : Infinity, hashIdx >= 0 ? hashIdx : Infinity);
  const suffix = input.slice(index);
  const path = resolve(from, '..', input);
  const ext = extname(path);
  const buffer = await readFile(path);
  const name = format.replace(/\[(name|hash|ext)(:(\d+))?\]/g, (substring, ...groups) => {
    const key = groups[0];
    const length = groups[2] == null ? undefined : parseInt(groups[2]);
    switch (key) {
      case 'name':
        return basename(path, ext).slice(0, length);
      case 'hash':
        return createHash('sha256').update(buffer).digest('hex').slice(0, length);
      case 'ext':
        return ext.slice(1, length);
    }
    return substring;
  });

  if (emitted[name] !== path) {
    await mkdir(destination, { recursive: true });
    await copyFile(path, join(destination, name));
    emitted[name] = path;
  }

  const src = baseUrl + name + suffix;
  if (!isImage) return src as T extends true ? ImageData : string;

  const metadata = await getImageMetadata(buffer);
  if (metadata == null) throw new Error(`invalid image: ${path}`);
  return { src, ...metadata } as T extends true ? ImageData : string;
}

/**
 * Retrieves metadata for an image buffer and generates a blurred version of the image.
 * @param buffer - The image buffer.
 * @returns The image metadata
 */
export async function getImageMetadata(buffer: Buffer): Promise<Omit<ImageData, 'src'> | undefined> {
  const img = sharp(buffer);
  const { format, width, height } = await img.metadata();
  if (format == null || width == null || height == null) return;
  if (!isValidImageFormat(format)) return;

  const aspectRatio = width / height;
  const blurWidth = 8;
  const blurHeight = Math.round(blurWidth / aspectRatio);
  const blurImage = await img.resize(blurWidth, blurHeight).webp({ quality: 1 }).toBuffer();
  const blurDataURL = `data:image/webp;base64,${blurImage.toString('base64')}`;
  return { format, height, width, blurDataURL, blurWidth, blurHeight, aspectRatio };
}

export function isValidImageFormat(format: keyof FormatEnum): format is ImageFormat {
  return (ImageFormats as readonly string[]).includes(format);
}
