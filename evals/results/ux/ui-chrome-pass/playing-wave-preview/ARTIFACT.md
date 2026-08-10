# Wave-preview reference strip — rendered evidence

Captured 2026-08-10 with Unity 6000.3.16f1 from the real `GameRoot` rendering path.
The temporary PlayMode probe imported the shipped StreamingAssets `L002.json`, launched it
through `GameRoot.LaunchWith`, waited two frames for Canvas/TMP layout, and rendered the live
camera into a Screen-matched `640x480` RenderTexture. The probe asserted the authored summary
`red x2|blue x2` and differentiated strip/board pixels before writing the PNG; it and its meta
were deleted before the product commit.

Reference input: the human-provided Gemini target at
`/Users/sushantsrikrish/Downloads/Gemini_Generated_Image_seqsafseqsafseqs.png`. The reference
was inspected directly and was not copied into this lane. Lane 1A owns its own copy and art
files; this lane used only its visual grammar.

## Inspection

The full frame was opened at original resolution. It shows one rounded Warm Paper tray in the
top status band, a red cat face paired with the circle badge and navy `x2`, and a blue cat face
paired with the square badge and navy `x2`. Both heads have visible ear silhouettes, eyes,
nose, and mouth marks. The counts are materially larger than the recorded bare-quad version
and remain contained by their entries. The underlying L002 board is visible below the strip.

PNG: `cm-ui-wave-reference-640x480.png`

SHA-256: `03f7c78201cfc1aecc4a68b2f3bb4d287b1d3fa6ea79c8141aacb146403c474b`

## Test trail

- Initial RED: `0/1`, missing `WavePreviewCanvas`,
  `/tmp/cm-ui-wave-ref-red.lVjCoY/results.xml`.
- Permanent visual + existing behavior GREEN: `2/2`,
  `/tmp/cm-ui-wave-final-green.I7Gzs9/results.xml`.
- Failure/device breadth GREEN: `17/17`,
  `/tmp/cm-ui-wave-breadth.yeBgPE/results.xml`.
- Symbol-node mutation RED: `0/1`, missing `WaveSymbol`,
  `/tmp/cm-ui-wave-mut-symbol.6o1tAG/results.xml`.
- Count-size mutation RED: `0/1`, 23dp was below the 24dp/38.25px floor,
  `/tmp/cm-ui-wave-mut-font.sVR3Dq/results.xml`.
- Deferred collider suite: `5/7` pass at `/tmp/cm-ui-wave-gated.zCM3CA/results.xml`; its two
  expected failures are exactly the inactive legacy Renderer and the retained
  `GameObject.CreatePrimitive` source site.

## Lane boundary

This visual increment retains exactly one `GameObject.CreatePrimitive` source site in
`WavePreviewStrip.cs`; its legacy renderer is disabled and never painted. Removing that path
is a separate future commit after Lane 1A's criterion-5 gate re-author reaches `main`.
