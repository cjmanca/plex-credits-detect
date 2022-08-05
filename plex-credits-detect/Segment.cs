using System.Diagnostics;

namespace plexCreditsDetect
{
    [DebuggerDisplay("Start: {start} - {end}, duration: {duration}")]
    public class Segment
    {
        public double start = 0;
        public double end = 0;
        public double duration => end - start;

        public bool isCredits = false;
        public bool isSilence = false;
        public bool isBlackframes = false;

        public Episode episode = null;

        public Segment()
        {

        }
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

        public bool Intersects(Segment seg, double permittedGap = 0, bool ignoreSection = false)
        {
            if (!ignoreSection)
            {
                if (seg.isSilence != isSilence || seg.isBlackframes != isBlackframes)
                {
                    return false;
                }
                else
                {
                    if (!isSilence && !isBlackframes && seg.isCredits != isCredits)
                    {
                        return false;
                    }
                }
            }

            Validate();
            seg.Validate();

            if (start <= seg.end + permittedGap && end >= seg.start - permittedGap)
            {
                return true;
            }
            return false;
        }

        public Segment Overlap(Segment seg, bool ignoreSection = false)
        {
            if (!ignoreSection)
            {
                if (seg.isSilence != isSilence || seg.isBlackframes != isBlackframes)
                {
                    return null;
                }
                else
                {
                    if (!isSilence && !isBlackframes && seg.isCredits != isCredits)
                    {
                        return null;
                    }
                }
            }

            Validate();
            seg.Validate();

            Segment result = new Segment(Math.Max(seg.start, start), Math.Min(seg.end, end));
            result.isCredits = isCredits;
            result.isSilence = isSilence;
            result.isBlackframes = isBlackframes;

            if (result.start >= result.end)
            {
                return null;
            }
            return result;
        }

        public override string ToString()
        {
            return $"Start: {start:0.00} - {end:0.00}, duration: {duration:0.00}";
        }
    }
}
