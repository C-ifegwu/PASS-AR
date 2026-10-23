using UnityEngine;

namespace PassAR
{
    /// <summary>
    /// All the screen colours in one place. Dark backgrounds with the orange of the
    /// Glads Apartment walls as the main accent colour. Text is kept easy to read.
    /// </summary>
    public static class Theme
    {
        public static readonly Color Background = new Color32(0x0D, 0x0F, 0x14, 0xFF);
        public static readonly Color Surface = new Color32(0x16, 0x19, 0x20, 0xE6);
        public static readonly Color Surface2 = new Color32(0x26, 0x2A, 0x33, 0xFF);
        public static readonly Color Accent = new Color32(0xFF, 0x7A, 0x2F, 0xFF);
        public static readonly Color Accent2 = new Color32(0xFF, 0xB2, 0x70, 0xFF);
        public static readonly Color OnAccent = new Color32(0x14, 0x10, 0x0C, 0xFF);
        public static readonly Color Text = new Color32(0xF4, 0xF5, 0xF7, 0xFF);
        public static readonly Color TextMuted = new Color32(0xA9, 0xAF, 0xBA, 0xFF);
        public static readonly Color Danger = new Color32(0xFF, 0x5C, 0x5C, 0xFF);
        public static readonly Color Success = new Color32(0x45, 0xE0, 0x9A, 0xFF);
    }
}
