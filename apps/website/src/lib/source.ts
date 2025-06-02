import { docs as allDocs, legal as allLegal } from '@/../.source';
import { loader } from 'fumadocs-core/source';
import { createMDXSource } from 'fumadocs-mdx';

export const legal = loader({
  baseUrl: '/legal',
  source: createMDXSource(allLegal),
});

export const docs = loader({
  baseUrl: '/docs',
  source: allDocs.toFumadocsSource(),
});
