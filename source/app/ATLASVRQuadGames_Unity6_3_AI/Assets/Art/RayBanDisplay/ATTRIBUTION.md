# Ray-Ban-style display asset attribution

Layout measurements and visual behavior were studied from Meta's public UI Toolkit for Meta Ray-Ban Display, pinned to commit `4409b3d832c822c54f1f5d122b5b629b6e7dab99`:
https://github.com/facebook/meta-ray-ban-display-ui-toolkit-web

The Unity components and geometric glyphs are original implementations. The toolkit's separately licensed SVG catalog is not included. App names and styling follow the original visual reference.

Typography uses Noto Sans, Copyright 2022 The Noto Project Authors, under the SIL Open Font License 1.1. See `Fonts/OFL.txt`. The Regular and Bold TTFs are static instances of Google's Noto Sans variable font at width 100 and weights 400/700, generated with fontTools. Source:
https://github.com/google/fonts/tree/main/ofl/notosans

Sample notification names/messages and 90%/82% battery values reproduce the supplied visual reference and are demonstration data, not connected account or device information. The date/time uses the local system clock. Quest passthrough composition and original geometric symbols differ from the physical glasses display.

## Feature screen samples

`Samples/gallery-coast.png`, `gallery-forest.png`, and `gallery-overlook.png` are unmodified demonstration images from `examples/gallery/public` in the same pinned Meta toolkit repository. The repository LICENSE and NOTICE are retained beside them. No separately licensed Meta icon package or Meta font files are included.

Native Unity feature compositions draw on the toolkit's Carousel, MediaWrapper, ButtonRail, ZoomIndicator, Scrubber, TextView, and mock messaging examples. MediaWrapper supplies a scrim/overlay pattern in the web toolkit; it is not a complete camera implementation. These Unity screens are original adaptations, not exported first-party application screens.

The visual prototype used sample contacts, routes and media controls. The integrated Quad Games rig also supports campus camera capture, music playback and waypoint navigation. See the [display integration record](../../../.agent-docs/xr-assistant-display.md) for current behavior.
