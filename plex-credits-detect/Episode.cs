using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace plexCreditsDetect
{
    public class Episode
    {

        // Saved in database
        public string id { get; set; }
        public string name { get; set; }
        public string dir { get; set; }
        public DateTime LastWriteTimeUtc { get; set; } = DateTime.MinValue;
        public long FileSize { get; set; } = 0;
        public bool DetectionPending { get; set; } = true;


        // Extra info
        public bool Exists { get; set; } = false;
        public string fullPath { get; set; }
        public string fullDirPath { get; set; }
        public Segments segments { get; set; } = new Segments();


        [DebuggerDisplay("Start: {start} - {end}, duration: {duration}")]
        public class Segment
        {
            public double start = 0;
            public double end = 0;
            public double duration => end - start;

            public Segment(double start, double end)
            {
                this.start = start;
                this.end = end;
            }

            public void Validate()
            {
                if (start > end)
                {
                    double temp = end;
                    end = start;
                    start = temp;
                }
            }

            public bool Intersects(Segment seg, double permittedGap = 0)
            {
                Validate();
                seg.Validate();

                if (start < seg.end + permittedGap && end > seg.start - permittedGap)
                {
                    return true;
                }
                return false;
            }

            public override string ToString()
            {
                return $"Start: {start:0.00} - {end:0.00}, duration: {duration:0.00}";
            }
        }

        public class Segments
        {
            public List<Segment> allSegments = new List<Segment>();

            public void AddSegment(Segment segment, double permittedGap = 0)
            {
                var segments = FindIntersectingSegments(segment, permittedGap);

                segments.Add(segment);

                double min = segments.Min(x => x.start);
                double max = segments.Max(x => x.end);

                segment = new Segment(min, max);

                foreach (var seg in segments)
                {
                    allSegments.Remove(seg);
                }

                allSegments.Add(segment);

                //allSegments.Sort((a, b) => a.start.CompareTo(b.start));
            }

            public List<Segment> FindIntersectingSegments(Segment segment, double permittedGap = 0)
            {
                List<Segment> ret = new List<Segment>();

                foreach (Segment seg in allSegments)
                {
                    if (segment.Intersects(seg, permittedGap))
                    {
                        ret.Add(seg);
                    }
                }

                return ret;
            }
        }

        public Episode()
        {

        }
        public Episode(string path)
        {
            ParseInfoFromPath(path);
        }

        void ParseInfoFromPath(string path)
        {
            Exists = false;

            FileInfo fi = new FileInfo(path);

            if (fi.Exists)
            {
                Exists = true;
                LastWriteTimeUtc = fi.LastWriteTimeUtc;
                FileSize = fi.Length;
            }
            else
            {
                foreach (var bPath in Program.settings.paths)
                {
                    string p = Program.PathCombine(bPath, path);
                    fi = new FileInfo(p);

                    if (fi.Exists)
                    {
                        Exists = true;
                        path = fi.FullName;
                        LastWriteTimeUtc = fi.LastWriteTimeUtc;
                        FileSize = fi.Length;

                        break;
                    }
                }
            }


            fullPath = path;
            fullDirPath = Path.GetDirectoryName(path);

            id = Program.getRelativePath(path);
            name = Path.GetFileName(path);
            dir = Program.getRelativeDirectory(path);

        }

    }
}
