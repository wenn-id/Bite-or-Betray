BITE OR BETRAY CHARACTER MULTI-VIEW

6 character folders, full body PNG 1024 x 1536 with a white background.
01_Front.png: original front view.
02_Left.png: left profile, nose pointing to the left of the image.
03_Back.png: back view.
04_Right.png: right profile, nose pointing to the right of the image.

Five characters have all four views. Folder 05_Red_Grouch has
front, back, and right. The left profile is not available yet because the generator
repeatedly rejected the result without a specific reason.

For Meshy, enable Multi-view and use the images from one character folder
for one run. Do not mix different characters in one set.

PromptSet.json contains the initial prompts. Corrections.json contains the correction
for the chef's back apron. AdditionalReferencePrompt.json contains the last attempt at
the red character's left profile. Manifest.json contains the source of the selected result files.
Generated with the built-in image_gen; reconstruction has not been tested in Meshy yet.
The back details are a continuation of the design from the front image, not a render
of a rotation of an existing 3D model.
