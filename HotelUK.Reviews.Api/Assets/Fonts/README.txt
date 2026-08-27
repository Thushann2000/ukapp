Drop three .ttf files in this folder so the Instagram graphic uses the same
typefaces as the web page:

  InstrumentSerif-Regular.ttf  -> https://fonts.google.com/specimen/Instrument+Serif
  InstrumentSerif-Italic.ttf   -> https://fonts.google.com/specimen/Instrument+Serif
  InstrumentSans-Regular.ttf   -> https://fonts.google.com/specimen/Instrument+Sans

Instrument Serif: press "Get font" then "Download all". The zip has both files
already named exactly as above. Put them here.

Instrument Sans: the same download gives you a variable font at the top level
and a "static" folder underneath. The one you want is

  static/InstrumentSans-Regular.ttf

Copy it here. Do not use the variable font - SkiaSharp will load it but always
draws it at the default width and weight, so it is no better and it is bigger.

If the files are missing the app still runs. It falls back to whatever fonts
the container has (Georgia is not there, so it lands on DejaVu Serif and DejaVu
Sans) and writes a warning to the log. The graphic will look noticeably
different from the web page, so it is worth doing this properly.

These two families replaced Fraunces and Karla when the page was rebuilt. If
you still have Fraunces-Bold.ttf, Fraunces-Italic.ttf or Karla-Regular.ttf in
this folder, delete them - nothing loads them any more.
