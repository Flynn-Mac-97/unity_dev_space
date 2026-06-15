# ScrollView Setup

ScrollRect requires a specific hierarchy to function correctly. Missing or misordered components cause issues.

**Required hierarchy:**
```
ScrollView (ScrollRect)
├── Viewport (RectTransform + Mask + Image)
│   └── Content (RectTransform + VerticalLayoutGroup + ContentSizeFitter)
│       └── [Children]
└── Scrollbar (optional)
```

**Setup rules:**
- **ScrollRect** component goes on the root ScrollView object.
- **Viewport** must have a `Mask` and an `Image` component.
- **Content** must have `ContentSizeFitter` with Vertical Fit set to "Preferred Size".
- **Dynamic population**: Clear existing children before populating to prevent duplication.

**Common failures:**
- Items duplicated → Content not cleared before populating.
- Not scrollable → missing `ContentSizeFitter` or `Mask`.
- Jitter → Layout Group conflict with `ContentSizeFitter`.
