import { defineCollection, z } from 'astro:content';

const releases = defineCollection({
  type: 'content',
  schema: z.object({
    version: z.string(),
    date: z.string(),
    type: z.enum(['Major', 'Feature', 'Fix']),
    summary: z.string(),
  }),
});

export const collections = { releases };
