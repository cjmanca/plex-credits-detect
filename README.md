# plex-credits-detect
Uses audio spectrographic fingerprinting (thanks to [AddictedCS/soundfingerprinting](https://github.com/AddictedCS/soundfingerprinting "AddictedCS/soundfingerprinting")) to analyze all episodes in a season and insert the credit timings into the plex intro database.
This provides a "Skip" button in plex for credits!
This doesn't replace the plex intro scanning, but is supplemental to it (and requires it to function, currently).

This tool is intended to stay running in the background all the time. It polls the plex database to check for new intro markers once per minute, and if found, will generate credits for those same episodes.

## ini options
A default ini file will be generated the first time you run the utility
```dosini
[directories]
TV = C:\path\to\library
Anime = C:\path\to\library
# place as many of these entries as you'd like for your plex libraries. The name before the = sign can be anything (no spaces)

[default]
useAudio = true # use audio fingerprinting
useVideo = false # use video frame fingerprinting (slow)

introMatchCount = 0 # how many extra intro sequences to find, not including the plex detected one
creditsMatchCount = 1 # how many credits sequences to find

introStart = 0 # percentage of show to start looking for intro at
introEnd = 0.5 # percentage of show to stop looking for intro
introMaxSearchPeriod = 900 # maximum seconds to look for intro (if smaller than introEnd - introStart)

creditsStart = 0.7 # percentage of show to start looking for credits at
creditsEnd = 1.0 # percentage of show to stop looking for credits
creditsMaxSearchPeriod = 600 # maximum seconds to look for credits (if smaller than creditsStart - creditsEnd)

shiftSegmentBySeconds = 2 # plex detected intros start about 2 seconds before the intro. If you'd like to reproduce 
											  # that, you would put a 2 here

minimumMatchSeconds = 20 # the minimum length of a duplicate section to be considered a valid match segment
PermittedGap = 2 # maximum non-matching seconds to be allowed inside a match
PermittedGapWithMinimumEnclosure = 5 # when considering combining multiple segments into one larger 
																	# segment, this is the maximum amount of seconds between them. 
																	# Each segment must be at least minimumMatchSeconds to be 
																	# considered for combining.

# see the soundfingerprint wiki page for more info on these: 
https://github.com/AddictedCS/soundfingerprinting/wiki/Algorithm-Configuration

audioAccuracy = 4 # called "ThresholdVotes" on the wiki
stride = 1024 
sampleRate = 5512
minFrequency = 100
maxFrequency = 2750

videoAccuracy = 2 # called "ThresholdVotes" on the wiki
videoSizeDivisor = 50 # 1080x1080 / videoSizeDivisor = video size used for comparisons
frameRate = 1 # biggest factor for video fingerprint speed and memory requirements

forceRedetect = false # if this is true, then it will ignore whether the file size matches the database when checking 
								   # if a redetect is needed. Useful if you change ini settings and want to force a regeneration 
								   # of the credits. After changing this, you'll need to do a plex dance to force plex to re-detect intros.

databasePath = C:\path\to\database\dir
PlexDatabasePath = C:\path\to\com.plexapp.plugins.library.db
ffmpegPath = C:\path\to\ffmpeg\bin
TempDirectoryPath = C:\path\to\empty\temp\dir
```


