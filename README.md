# plex-credits-detect
Uses audio spectrographic fingerprinting (thanks to [AddictedCS/soundfingerprinting](https://github.com/AddictedCS/soundfingerprinting "AddictedCS/soundfingerprinting")) to analyze all episodes in a season and insert the credit timings into the plex intro database.
This provides a "Skip" button in plex for credits!
This doesn't replace the plex intro scanning, but is supplemental to it.

Also can attempt to detect video frames that are mostly black to find classic style credits in movies.

This tool is intended to stay running in the background all the time. It polls the plex database to check for new intro markers once per minute, and if found, will generate credits for those same episodes.

***Make sure you backup your plex database before running!*** While I've never had an issue - it is modifying the plex db to insert the intro/credit timings, so the possibility of corruption exists.

## Installation
![Docker Pulls][badge-docker-pulls]
![Downloads][badge-downloads]

[badge-docker-pulls]: https://img.shields.io/docker/pulls/cjmanca/plex-credits-detect?style=flat-square
[badge-downloads]: https://img.shields.io/github/downloads/cjmanca/plex-credits-detect/total?style=flat-square

### Native
Download a [release](https://github.com/cjmanca/plex-credits-detect/releases "Releases"), extract anywhere you'd like. The first time you run it, a default config file will be generated, and the path to the config file will appear in the console window.

To run on linux/MacOS:
```dotnet plex-credits-detect.dll```

To run on Windows, just launch the plex-credits-detect.exe, although you may want to do it from command prompt so you can see the path of the config file.

Edit the config file as described below.

Run it again and if configured correctly, it should start working it's way through your library.

This tool is intended to run in the background all the time, monitoring for new episodes and automatically generating credits for them as needed, so you'll want to set it to run at startup.

### Via Docker
```bash
docker run -d --restart unless-stopped \
    -v /local/config/location:/config \
    -v "/path/to/Plex Media Server/Plug-in Support/Databases":/PlexDB \
    -v /media:/media \
    -it cjmanca/plex-credits-detect:main
```
***Docker on windows will encounter issues due to the way docker handles volume mounts on windows, which doesn't allow proper SQLite locking. There's nothing I can do about this, so currently it's recommended to run natively on Windows***.

## ini options
A default global ini file will be generated the first time you run the utility.

ini files are read in the directory structure, which can override the default ini. These can be partial (having only one or two entries changed), allowing you to just override what you need for a particular library, show or season.

Given an episode with the path of:
- C:\media\anime\Overlord\Season 1\Episode 1.mkv

The ini would be read in this order:
1. Global fingerprint.ini
2. C:\fingerprint.ini
3. C:\media\fingerprint.ini
4. C:\media\anime\fingerprint.ini
5. C:\media\anime\Overlord\fingerprint.ini
6. C:\media\anime\Overlord\Season 1\fingerprint.ini

Each ini that is encountered will override any provided options from all previous files. All except the global fingerprint.ini are optional.

```dosini
[config]
stopProgramAfterRun = false
# When false, it will automatically scan when a new file is detected in directories.
# When true, it will stop program after first scan of all directories

[paths]
databasePath = C:\path\to\database\dir                        # Where to save the plex-credits-detect database
PlexDatabasePath = C:\path\to\com.plexapp.plugins.library.db  # Full path to the Plex sqlite database
ffmpegPath = C:\path\to\ffmpeg\bin\ffmpeg.exe                 # ffmpeg invocation. Can just be "ffmpeg" if it's in your path
TempDirectoryPath = C:\path\to\empty\temp\dir                 # Where to store temp files


[directories]
C:\path\to\library = C:\path\to\library
C:\path\credits\scanner\sees = C:\path\plex\server\sees

# If not using docker containers, the directories section can be left blank
# These let you remap paths if using docker with different path mappings than plex sees
# Place as many of these entries as you'd like for your plex libraries. 
# The first path is the local (internal container) path. The second path is the path the Plex server sees.
# The plex side of these paths must be the same as configured in plex in order to properly locate the files


[intro]
introStart = 0               # percentage of show to start looking for intro at
introEnd = 0.5               # percentage of show to stop looking for intro
introMaxSearchPeriod = 900   # maximum seconds to look for intro (if smaller than introEnd - introStart)


[credits]
creditsStart = 0.7           # percentage of show to start looking for credits at
creditsEnd = 1.0             # percentage of show to stop looking for credits
creditsMaxSearchPeriod = 600 # maximum seconds to look for credits (if smaller than creditsStart-creditsEnd)


[matching]
useAudio = true                      # use audio fingerprinting
useVideo = false                     # use video frame fingerprinting (slow)
introMatchCount = 0          # how many extra intro sequences to find, not including the plex detected one
creditsMatchCount = 1        # how many credits sequences to find
quickDetectFingerprintSamples = 5     # Try this many fingerprints if only matching a small number of episodes
fullDetectFingerprintMaxSamples = 10  # When doing a "full" match, (or if quick match fails to find a match)
                                      # restrict to a maximum of this many fingerprints
                                      
# see the soundfingerprint wiki page for more info on these: 
# https://github.com/AddictedCS/soundfingerprinting/wiki/Algorithm-Configuration

audioAccuracy = 4     # called "ThresholdVotes" on the wiki
stride = 512          # If scanning is too slow, can set this to 1024 or 2048 to speed up. May miss sections
sampleRate = 5512
minFrequency = 100
maxFrequency = 2750
videoAccuracy = 2     # called "ThresholdVotes" on the wiki
videoSizeDivisor = 50 # 1080x1080 / videoSizeDivisor = video size used for comparisons
frameRate = 1         # biggest factor for video fingerprint speed and memory requirements


[silence]
detectSilenceAfterCredits = true     # check for long periods of silence after the credits
silenceDecibels = -55 # If the volume is below this for longer than minimumMatchSeconds it'll detect as silence


[blackframes]
detectBlackframes = true                       # Scans video frames for a majority black background (typical credits)
blackframeOnlyMovies = true                    # If true, will only scan shows in movie libraries
blackframeUseMaxSearchPeriodForEpisodes = true # Whether to restrict via "creditsMaxSearchPeriod" for episodes
blackframeUseMaxSearchPeriodForMovies = false  # Whether to restrict via "creditsMaxSearchPeriod" for movies
blackframeScreenPercentage = 75                # The percentage of the screen that must be black to count as a black frame
blackframePixelPercentage = 2                  # Percentage between 0 (absolute black) and 100 (white) that
                                               # is considered to be a "black" pixel
blackframeMovieMinimumMatchSeconds = 20        # the minimum length of a duplicate section to be 
                                               # considered a valid match segment when searching for black frames


[timing]
shiftSegmentBySeconds = 2    # plex detected intros start about 2 seconds before the intro. If you'd like to  
                             # reproduce that, you would put a 2 here
minimumMatchSeconds = 20     # the minimum length of a duplicate section to be considered a valid match segment
PermittedGap = 2                      # maximum non-matching seconds to be allowed inside a match
PermittedGapWithMinimumEnclosure = 5  # when considering combining multiple segments into one larger 
                                      # segment, this is the maximum amount of seconds between them. 
                                      # Each segment must be at least minimumMatchSeconds to be 
                                      # considered for combining.


[monitoring]
monitorPlexIntros = true           # Whether to check the plex DB for new plex intros. Not necessary if only using for movies
monitorDirectoryChanges = true     # Whether to monitor for directory changes.


[redetection]
crawlDirectoriesOnStartup = false   # When true, scans your libraries for ini files that may override
                                    # the other recheck settings here on a per-directory basis.
                                    # This only works in conjunction with other ini files
                                    # placed in your media directories to "target scan"
recheckBlackframesOnStartup = false # When true, scans your libraries for shows that haven't yet
                                    # been checked for black frames.
                                    # Keep set to false during normal operation.
recheckSilenceOnStartup = false     # When true, scans your libraries for shows that haven't yet
                                    # been checked for sections of silence after the credits
                                    # Keep set to false during normal operation.
                                    # This will also pick up episodes that don't have plex detected intros.
recheckUndetectedOnStartup = false  # When true, scans your libraries for episodes that are missing timings
                                    # This can be useful if you change ini settings and want to rescan
                                    # all your libraries to try to find missing credits
                                    # Keep set to false during normal operation.
                                    # This will also pick up episodes that don't have plex detected intros.
forceRedetect = false # if this is true, then it will ignore whether the file size matches the database when  
                      # checking if a redetect is needed. Useful if you change ini settings and want to   
                      # force a regeneration of the credits. After changing this, you'll need to do a plex 
                      # dance to force plex to re-detect intros.
redetectIfFileSizeChanges = true   # Whether to discard previous matches and check fresh if the file size is
                                   # different than the last time the episode was scanned
                                   # Turn off if using a tool like Tdarr to reencode without changing timings

```


