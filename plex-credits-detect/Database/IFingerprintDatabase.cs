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
        public void InsertHash(Episode trackinfo, AVHashes hashes);
        public void CloseDatabase();
        public Episode GetEpisode(string id);
        public AVHashes GetTrackHash(string id);
        public void DeleteEpisode(string id);
        public List<Episode> GetPendingEpisodes();
        public Episode GetOnePendingEpisode();
    }
}
