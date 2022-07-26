using SoundFingerprinting;
using SoundFingerprinting.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace plexCreditsDetect.Database
{
    internal interface IFingerprintDatabase
    {
        public IModelService GetModelService();
        public void LoadDatabase(string path);
        public void SetupNewScan();
        public void Insert(Episode trackinfo);
        public void InsertHash(Episode ep, AVHashes hashes, MediaType avtype, bool isCredits, double start, int partNum = -1);
        public void CloseDatabase();
        public Episode GetEpisode(string id);
        public AVHashes GetTrackHash(string id, bool isCredits, int partNum = -1);
        public void DeleteEpisode(Episode ep);
        public List<Episode> GetPendingEpisodes();
        public Episode GetOnePendingEpisode();
        public List<string> GetPendingDirectories();
        public void InsertTiming(Episode ep, Segment segment, bool isPlexIntro);
        public void DeleteEpisodeTimings(Episode ep);
    }
}
