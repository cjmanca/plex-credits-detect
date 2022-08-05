namespace plexCreditsDetect
{
    public class Segments
    {
        public List<Segment> allSegments = new List<Segment>();

        public void AddSegment(Segment segment, double permittedGap = 0, bool ignoreSection = false)
        {
            var segments = FindIntersectingSegments(segment, permittedGap, ignoreSection);

            segments.Add(segment);

            double min = segments.Min(x => x.start);
            double max = segments.Max(x => x.end);

            var newSeg = new Segment(min, max);
            newSeg.isCredits = segment.isCredits;
            newSeg.isSilence = segment.isSilence;
            newSeg.isBlackframes = segment.isBlackframes;

            foreach (var seg in segments)
            {
                allSegments.Remove(seg);
            }

            allSegments.Add(newSeg);

            //allSegments.Sort((a, b) => a.start.CompareTo(b.start));
        }

        public List<Segment> FindIntersectingSegments(Segment segment, double permittedGap = 0, bool ignoreSection = false)
        {
            List<Segment> ret = new List<Segment>();

            foreach (Segment seg in allSegments)
            {
                if (segment.Intersects(seg, permittedGap, ignoreSection))
                {
                    ret.Add(seg);
                }
            }

            return ret;
        }

        public Segments FindAllOverlaps(Segments other, bool ignoreSection = false)
        {
            Segments ret = new Segments();

            foreach (var x in other.allSegments)
            {
                foreach (var y in allSegments)
                {
                    Segment seg = x.Overlap(y, ignoreSection);
                    if (seg != null)
                    {
                        ret.AddSegment(seg, 0, ignoreSection);
                    }
                }
            }

            return ret;
        }
    }
}
