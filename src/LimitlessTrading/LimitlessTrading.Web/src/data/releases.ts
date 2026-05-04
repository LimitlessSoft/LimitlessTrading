export const CURRENT_VERSION = '1.0.0';

export interface PlatformRelease {
  platform: 'Windows' | 'macOS' | 'Linux';
  label: string;
  ext: string;
  size: string;
  url: string;
  checksum: string;
}

export const platformReleases: PlatformRelease[] = [
  {
    platform: 'Windows',
    label: 'Windows',
    ext: '.exe',
    size: '—',
    url: '#',
    checksum: '—',
  },
  {
    platform: 'macOS',
    label: 'macOS',
    ext: '.dmg',
    size: '—',
    url: '#',
    checksum: '—',
  },
  {
    platform: 'Linux',
    label: 'Linux',
    ext: '.AppImage',
    size: '—',
    url: '#',
    checksum: '—',
  },
];
