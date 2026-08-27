# Hotel UK Passikudah — Review System Setup Guide

Follow the sections in order. Do not skip section 3, it is the one that trips everybody up.

---

## 1. What this system does

A guest fills in a form. The moment they press **Send my review**, this happens:

1. Their review text is copied to their clipboard, and a thank-you box appears with buttons for Google and Trip.com. They just paste and post.
2. If they gave **4 or 5 stars**, the text is posted to your **Facebook Page**, and a square picture of the review is drawn by the server and posted to your **Instagram**.
3. If they gave **1 to 3 stars**, nothing is posted anywhere. The review is sent to you privately instead — see section 5. The page tells the guest this is what is happening, and still offers them the Google and Trip.com links. Nobody is stopped from posting publicly.

Nothing is saved anywhere. No database, no files kept. The review passes through and is gone.

One address may send **six reviews an hour**. That is generous for a real guest and useless to somebody with a script pointed at your Facebook Page.

**One thing you should know up front:** Google and Trip.com do not allow anybody to post reviews on a guest's behalf. That is why those two are copy-and-paste buttons and not automatic. There is no way around this, for anyone.

---

## 2. What you need before you start

- A **Facebook Page** for the hotel (not a personal profile).
- An **Instagram account** switched to **Business** or **Creator**, and **linked to that Facebook Page**.
  In the Instagram app: Settings → Account type and tools → Switch to professional account → then link the Page.
- A **Meta developer app**: go to <https://developers.facebook.com/apps> → Create app → choose **Business** → add the **Facebook Login for Business** product.
- **.NET 9 SDK** on your computer, and the **Heroku CLI**.

If Instagram is not linked to the Page, the Instagram half will never work. Check this first.

---

## 3. Get your four numbers

Open the **Graph API Explorer**: <https://developers.facebook.com/tools/explorer>

### 3a. Give yourself the permissions

Pick your app at the top right. Click **Add a permission** and tick these five:

| Permission | Why you need it |
|---|---|
| `pages_show_list` | To see your Pages |
| `pages_read_engagement` | To read the Page |
| `pages_manage_posts` | **To post to the Page** |
| `instagram_basic` | To see the linked Instagram account |
| `instagram_content_publish` | **To post to Instagram** |

Click **Generate Access Token** and approve. You now have a *short-lived* token that dies in about an hour. Keep going.

### 3b. Turn it into a token that never expires

Run this in the Explorer's address bar (swap in your own values, App Secret is in your app's Settings → Basic):

```
GET /v25.0/oauth/access_token
    ?grant_type=fb_exchange_token
    &client_id=YOUR_APP_ID
    &client_secret=YOUR_APP_SECRET
    &fb_exchange_token=THE_SHORT_TOKEN_FROM_3a
```

You get back a **long-lived user token** (about 60 days). Now run:

```
GET /v25.0/me/accounts?access_token=THE_LONG_LIVED_USER_TOKEN
```

The `access_token` in that answer is your **Page Access Token**, and this one **does not expire**. The `id` next to it is your **Page ID**. Write both down.

### 3c. Get the Instagram ID

```
GET /v25.0/YOUR_PAGE_ID?fields=instagram_business_account&access_token=YOUR_PAGE_TOKEN
```

The number that comes back is your **Instagram Business Account ID**.

You should now have: Page ID, Page Access Token, Instagram ID. The fourth number comes in section 6 — it is your Heroku web address.

---

## 4. Put the fonts and the photographs in place

### 4a. Fonts

The Instagram graphic is drawn by the server, and it should look like the same piece of work as the web page. Both use **Instrument Serif** and **Instrument Sans**.

Download them and put three files in `HotelUK.Reviews.Api/Assets/Fonts/`:

```
InstrumentSerif-Regular.ttf
InstrumentSerif-Italic.ttf
InstrumentSans-Regular.ttf
```

Full instructions are in `Assets/Fonts/README.txt`. If you have old `Fraunces-*.ttf` or `Karla-Regular.ttf` files sitting there, delete them — nothing loads them any more.

Skip this and the app still runs, but the graphic falls back to DejaVu and stops matching the page.

### 4b. Photographs

Put two files in `HotelUK.Reviews.Api/wwwroot/img/`:

```
bay.jpg          the wide one, for laptops
bay-mobile.jpg   a tighter crop, for phones
```

With those names you do not edit any code. Full details, including where to get pictures you are allowed to use, are in `wwwroot/img/README.txt`.

Optional: a silent 8 to 15 second loop saved as `bay.mp4` in the same folder plays behind the title on its own.

### 4c. Your two links

Open `HotelUK.Reviews.Api/wwwroot/index.html`, find the `CONFIG` block near the bottom, and fill in your Google review link and your Trip.com page address.

`publishThreshold` in that same block must match `MinimumRatingToPublish` in the next section. If the two disagree, the page will tell a guest one thing and the server will do another.

---

## 5. Fill in `appsettings.json`

Open `HotelUK.Reviews.Api/appsettings.json` and fill in the `Meta` section:

```json
"Meta": {
  "GraphApiVersion": "v25.0",
  "PageId": "123456789012345",
  "PageAccessToken": "EAAG...the long one...",
  "InstagramUserId": "178414...",
  "InstagramAccessToken": "",
  "PublicBaseUrl": "https://your-app-name.herokuapp.com",

  "MinimumRatingToPublish": 4,
  "PrivateFeedbackWebhookUrl": "",

  "PostToFacebook": true,
  "PostToInstagram": true,
  "InstagramContainerTimeoutSeconds": 45,

  "PreviewEnabled": true,
  "MaxSubmissionsPerHourPerIp": 6
}
```

What each of the interesting ones does:

- **`InstagramAccessToken`** — leave it empty. The Page token is used for both.
- **`MinimumRatingToPublish`** — set to `4`. A guest who gives 2 stars still gets a warm thank-you, but their review is not put on your pages. Set it to `1` if you want everything published. **Whatever you choose, put the same number in `publishThreshold` in `index.html`.**
- **`PrivateFeedbackWebhookUrl`** — **set this up.** It is where a review too low to publish is sent. Any address that accepts a JSON POST works: a Slack incoming webhook, a Google Chat webhook, a Discord webhook, a Zapier or Make catch hook that turns it into an email or a WhatsApp message.

  Leave it empty and a 2-star review only appears in `heroku logs`, which nobody reads. The page promises the guest their words reach the manager. Keep that promise.

  The system has no database, so this webhook is the *only* copy of a low review. If it fails, the review is gone.
- **`PreviewEnabled`** — leave it `true` while you tune the graphic, then set it to `false`. That endpoint puts any words a stranger types onto your hotel's branding.
- **`MaxSubmissionsPerHourPerIp`** — how many reviews one internet address may send in an hour. `6` is fine. If the whole hotel is behind one wifi router, every guest looks like the same address, so raise it if a busy checkout morning starts hitting the limit.

**Never put the real token in a file you push to GitHub.** For local testing use `appsettings.Development.json` instead (already in `.gitignore` and `.dockerignore`). On Heroku the tokens live in Config Vars, section 6.

---

## 6. Deploy to Heroku

Heroku does not support .NET on its own, so we ship a container. That is what the `Dockerfile` and `heroku.yml` are for.

```bash
heroku login

# create the app in container mode
heroku create hotel-uk-reviews --stack=container

# put the secrets in. Note the DOUBLE underscore - that is how .NET
# reads "Meta:PageId" from an environment variable.
heroku config:set Meta__PageId="123456789012345" -a hotel-uk-reviews
heroku config:set Meta__PageAccessToken="EAAG..." -a hotel-uk-reviews
heroku config:set Meta__InstagramUserId="178414..." -a hotel-uk-reviews
heroku config:set Meta__PublicBaseUrl="https://hotel-uk-reviews.herokuapp.com" -a hotel-uk-reviews
heroku config:set Meta__MinimumRatingToPublish="4" -a hotel-uk-reviews
heroku config:set Meta__PrivateFeedbackWebhookUrl="https://hooks.slack.com/services/..." -a hotel-uk-reviews

git init
git add .
git commit -m "Hotel UK Passikudah review system"
heroku git:remote -a hotel-uk-reviews
git push heroku main
```

When it finishes, open `https://hotel-uk-reviews.herokuapp.com` — the form is served by the same app, so there is no CORS to configure and nothing else to host.

Three Heroku-specific things to remember:

1. **`Meta__PublicBaseUrl` must be your real Heroku address.** Instagram does not accept an uploaded file — it downloads the picture from a public link. The app writes the PNG into `wwwroot/generated/`, gives Instagram the link, and deletes it straight after. If this setting is wrong or says `localhost`, Instagram will fail every time.
2. **Stay on one web dyno.** Heroku's disk is per-dyno. With two dynos, the dyno that made the picture may not be the dyno Instagram asks for it. If you ever need to scale up, move the picture to Cloudinary or S3 — there is a comment in `MetaPublisherService.cs` marking the exact spot to change.
3. **Eco dynos fall asleep** after 30 minutes of no traffic. The first guest of the day waits a few seconds. A Basic dyno (about USD 7/month) stays awake.

Check it is alive: `https://your-app.herokuapp.com/api/reviews/health`

That page also tells you whether the private feedback webhook is configured (`privateFeedbackConfigured`). It never shows a token.

---

## 7. Test before you tell guests about it

**See the Instagram picture without posting anything:**

```
https://your-app.herokuapp.com/api/reviews/preview?name=Amara%20Silva&rating=5&text=The%20bay%20was%20perfect
```

That draws the graphic and shows it in your browser. Change the text and reload until you like it. **Then set `Meta__PreviewEnabled=false`** — leaving it open lets anybody put any sentence they like onto your hotel's branding and share the picture.

**Then do three real end-to-end tests** with your own name:

- a **5-star** one — should land on Facebook and Instagram
- a **2-star** one — should land in your Slack / Chat / email and **nowhere public**
- one with **an emoji and an accented name** — should draw cleanly on the graphic

Watch the log while they run:

```bash
heroku logs --tail -a hotel-uk-reviews
```

---

## 8. About Meta App Review

While your app is in **Development mode**, posting works for people who are admins of the app and the Page. That is usually enough for a hotel posting to its own accounts, and many small properties run exactly like this.

To move the app to **Live** mode you must submit `pages_manage_posts` and `instagram_content_publish` for App Review, and complete **Business Verification** (Meta asks for your business registration papers). Budget a couple of weeks for this. Start it early if you want the app in Live mode.

---

## 9. Errors you are likely to meet

| What you see | What it means |
|---|---|
| `Unable to load shared library 'libSkiaSharp'` | Running on Linux without the native package. The `Dockerfile` installs it — make sure you deployed with the container stack, not a buildpack. |
| `Could not create /app/wwwroot/generated` in the log | The folder is not writable by the user Heroku runs the container as. The `Dockerfile` chmods it; make sure you deployed the current one. |
| Instagram never finishes, then times out | Instagram could not download your picture. Check `Meta__PublicBaseUrl` opens in a browser, and that `/generated/...png` is reachable. |
| `(#200) requires pages_manage_posts` | The token was made without that permission. Redo section 3a and generate a fresh token. |
| `The user is not an Instagram Business account` | The Instagram account is still Personal, or not linked to the Page. |
| Text on the picture shows as boxes | The font files are missing. Add the three `.ttf` files listed in section 4a. |
| Emoji missing from the picture | On purpose. No font in the container can draw them, so they would be empty boxes. The full text with the emoji still goes in the Facebook and Instagram captions. |
| Clipboard copy does nothing | The clipboard only works on `https` (or `localhost`). Heroku gives you https, so this only happens in local testing. |
| `HTTP 429` when you submit | The rate limit. Six an hour from one address. Raise `Meta__MaxSubmissionsPerHourPerIp` if guests share one wifi connection. |
| A 2-star review vanished | `PrivateFeedbackWebhookUrl` is empty. It is in `heroku logs` as `LOW RATING`, and nowhere else. Set the webhook. |

---

## 10. What changed in this version

- The review page was rebuilt: new layout, **Instrument Serif + Instrument Sans** in place of Fraunces + Karla, and a slightly deeper palette. The graphic generator was updated to match, so the two still look like one family.
- The photograph is now set on the `<picture>` tag in the hero, not the old `--photo` CSS variable. Phones load a smaller file.
- **Low ratings now go somewhere.** They used to be dropped with a line in the log. There is a private webhook for them, and the page tells the guest honestly what is happening.
- **`X-Forwarded-Proto` now works.** ForwardedHeaders only trusts loopback by default, and Heroku's router is not loopback, so the app used to think every request was plain `http`.
- **Rate limiting** on the submit endpoint, so nobody can script your Facebook Page full of posts.
- **Response compression** and cache headers, which take the page from about 55 KB to about 12 KB on the wire.
- CORS no longer allows any origin by default. It is only switched on if you list origins in `Cors:AllowedOrigins`.
- The preview endpoint can be turned off.
- `"VERIFIED GUEST"` on the graphic became `"GUEST REVIEW"` — the form is open to anyone, so the hotel cannot stand behind the first one.
- Reviews in non-Latin scripts render properly; overlong words wrap instead of running off the edge; temp-file cleanup uses last-write time, which is what Linux actually keeps.
