PICTURES AND CLIPS FOR THE REVIEW PAGE
======================================

The page takes one picture and, if you want, one short clip. Both are
optional - without them a drawn beach scene is used.


1. A PICTURE
------------
Best option: a photo of Hotel UK Passikudah's own beach. Guests recognise
it, and it is yours to use.

Save TWO files in this folder:

    bay.jpg          1920 x 1200 or larger, landscape, under 400 KB
    bay-mobile.jpg   about 900 x 1100, cropped tighter, under 200 KB

Phones load the small one, everything else loads the big one. That is
the whole reason for two files - it is the single biggest thing you can
do to make the page open fast on a guest's phone.

If the names are the same as above, you do not have to touch index.html
at all. The blurred background further down the page follows the same
picture on its own.

To use different names, open index.html, find the block in the hero that
starts with <picture> and change the two file names there. That is the
only place. Nothing else in the file needs editing.


2. A CLIP, FOR MOVING WATER
---------------------------
Save a short video here as  bay.mp4  and the page plays it behind the
title on its own. No code change needed.

  - 8 to 15 seconds, looping, no sound
  - 1920 x 1080, H.264, under about 4 MB
  - film the sea from a fixed position so the loop is not jumpy

If bay.mp4 is not here, the page removes the video and shows the picture.


3. A LINK TO A PAGE IS NOT A LINK TO A PICTURE
----------------------------------------------
This is the mistake that catches everybody.

When you find a photo on a stock site, the address in your browser bar
looks like this:

    https://somesite.com/free-photo/beautiful_1030690.htm

That is the PAGE that shows the photo. It ends in .htm. Pointing the
page at it does nothing, because the browser is being asked to paint a
picture and it gets a web page instead.

What the page needs is the picture file itself - something ending in
.jpg, .png or .webp.

And even when you find that address, most stock sites refuse to serve
their pictures to other websites. So the reliable route is always:

    open the page  ->  press Download  ->  rename the file to bay.jpg
    ->  put it in this folder

If the picture is not loading, open the page in Chrome, press F12, and
look at the Console tab. The page prints a plain message telling you
which of these two problems it is.


4. WHERE TO GET PICTURES YOU MAY USE
------------------------------------
Free for commercial use, no credit required:

    https://unsplash.com        search: passikudah, sri lanka beach, turquoise sea
    https://www.pexels.com      search: tropical beach drone, calm sea
    https://pixabay.com         search: beach aerial

On Unsplash and Pexels, open the photo, press Download, then rename the
file to bay.jpg and put it in this folder.

Check the licence before you use anything. Some free sites let you use
photos commercially with no conditions. Others ask you to credit the
photographer. If yours does, index.html has a line ready for it in the
footer - search for "photo-credit", put the wording in, and remove the
word "hidden".

Do NOT use a photo taken from another hotel's website. Those pictures
belong to that hotel, and using a competitor's beach on this page is
both a copyright problem and bad for the hotel's own name.
