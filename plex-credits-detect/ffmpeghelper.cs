using FFmpeg.AutoGen;
using FFmpeg.AutoGen.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace plexCreditsDetect
{
    internal unsafe class ffmpeghelper
    {
        public static double GetDuration(string path)
        {
            var pFormatContext = ffmpeg.avformat_alloc_context();

            ffmpeg.avformat_open_input(&pFormatContext, path, null, null);

            if (pFormatContext == null)
            {
                return 0;
            }

            long ret = pFormatContext->duration;

            if (ret <= 0)
            {
                ffmpeg.avformat_find_stream_info(pFormatContext, null);
                ret = pFormatContext->duration;
            }

            ffmpeg.avformat_close_input(&pFormatContext);
            ffmpeg.avformat_free_context(pFormatContext);

            return ret / (double)ffmpeg.AV_TIME_BASE;
        }

        private static string Execute(string exePath, string parameters, out string errors)
        {
            string result = String.Empty;
            errors = String.Empty;

            using (Process p = new Process())
            {
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.FileName = exePath;
                p.StartInfo.Arguments = parameters;
                p.Start();
                p.WaitForExit();

                result = p.StandardOutput.ReadToEnd();
                errors = p.StandardError.ReadToEnd();
            }

            return result;
        }

        public static Segments DetectSilence(string in_filename, double minimumSeconds, int silenceDecibels)
        {
            string errors = "";

            string args = $"-i \"{in_filename}\" -v fatal -threads 0 -filter:a:0 silencedetect={silenceDecibels}dB:d={minimumSeconds},ametadata=mode=print:file=-:key=lavfi.silence_start,ametadata=mode=print:file=-:key=lavfi.silence_end -vn -sn -dn -f null -";

            string output = Execute(Program.settings.ffmpegPath, args, out errors);

            Segments segments = new Segments();

            List<double> startTimes = new List<double>();
            List<double> endTimes = new List<double>();

            string[] lines = output.Split(
                new string[] { "\r\n", "\r", "\n" },
                StringSplitOptions.RemoveEmptyEntries
            );


            foreach (string line in lines)
            {
                if (line.Contains("silence_start"))
                {
                    string tmp = line.Substring(line.LastIndexOf('=') + 1);
                    double dtmp = 0;

                    if (double.TryParse(tmp.Trim(), out dtmp))
                    {
                        startTimes.Add(dtmp);
                    }
                }
                if (line.Contains("silence_end"))
                {
                    string tmp = line.Substring(line.LastIndexOf('=') + 1);
                    double dtmp = 0;

                    if (double.TryParse(tmp.Trim(), out dtmp))
                    {
                        endTimes.Add(dtmp);
                    }
                }
            }

            if (endTimes.Count() < startTimes.Count())
            {
                endTimes.Add(GetDuration(in_filename));
            }
            if (endTimes.Count() != startTimes.Count())
            {
                Console.WriteLine("DetectSilence - different start/end counts");
                return segments;
            }

            startTimes.Sort();
            endTimes.Sort();

            for (int i = 0; i < startTimes.Count(); i++)
            {
                Segment seg = new Segment();

                seg.isSilence = true;
                seg.start = startTimes[i];
                seg.end = endTimes[i];

                if (seg.end < seg.start)
                {
                    Console.WriteLine("DetectSilence - end earlier than start:");
                    foreach(var item in endTimes)
                    {
                        Console.WriteLine("Start: " + item);
                    }
                    foreach (var item in endTimes)
                    {
                        Console.WriteLine("End: " + item);
                    }
                    return segments;
                }

                segments.AddSegment(seg);
            }

            return segments;
        }

        public static bool CutVideo(double from_seconds, double end_seconds, string in_filename, string out_filename, bool includeVideo, bool includeAudio, int sampleRate)
        {
            /**/
            string endian = BitConverter.IsLittleEndian ? "le" : "be";

            string noAudio = includeAudio ? $"-map 0:a:0 -c:a pcm_s16{endian} -ar {sampleRate} -ac 1" : "-an";
            string noVideo = includeVideo ? "-map 0:v:0 -vcodec copy" : "-vn";

            string args = $"-y -loglevel fatal -threads 0 -ss {from_seconds} -i \"{in_filename}\" -to {end_seconds} {noVideo} {noAudio} -sn -dn \"{out_filename}\"";

            /** /

            string noAudio = includeAudio ? "" : "-an";
            string noVideo = includeVideo ? "" : "-vn";

            string args = $"-y -loglevel error -ss {from_seconds} -i \"{in_filename}\" -to {end_seconds} {noVideo} {noAudio} -sn -dn -c copy -copyts \"{out_filename}\"";

            /**/
            string errors;
            string output = Execute(Program.settings.ffmpegPath, args, out errors);

            if (output != "" || errors != "")
            {
                Console.WriteLine("ffmpeg error: " + errors + " output: " + output);
                return false;
            }

            return true;

            // The code below fails at avformat_write_header for some videos, but not sure why. Just going to use command line for now

            // ported from ffmpeg remuxing.c example and more specifically:
            // https://stackoverflow.com/questions/20856803/how-to-cut-video-with-ffmpeg-c-api

            AVOutputFormat* ofmt = null;
            AVFormatContext* ifmt_ctx = null;
            AVFormatContext* ofmt_ctx = null;
            AVPacket pkt;
            int ret = 0, i;

            ffmpeg.av_register_all();

            try
            {
                if ((ret = ffmpeg.avformat_open_input(&ifmt_ctx, in_filename, null, null)) < 0) {
                    Console.WriteLine($"Could not open input file '{in_filename}'");
                    return false;
                }

                if ((ret = ffmpeg.avformat_find_stream_info(ifmt_ctx, null)) < 0) {
                    Console.WriteLine("Failed to retrieve input stream information");
                    return false;
                }

                ffmpeg.av_dump_format(ifmt_ctx, 0, in_filename, 0);

                ffmpeg.avformat_alloc_output_context2(&ofmt_ctx, null, null, out_filename);
                if (ofmt_ctx == null)
                {
                    Console.WriteLine("Could not create output context");
                    ret = ffmpeg.AVERROR_UNKNOWN;
                    return false;
                }

                ofmt = ofmt_ctx->oformat;

                for (i = 0; i < ifmt_ctx->nb_streams; i++)
                {
                    AVStream* in_stream = ifmt_ctx->streams[i];
                    AVStream* out_stream = ffmpeg.avformat_new_stream(ofmt_ctx, in_stream->codec->codec);
                    if (out_stream == null)
                    {
                        Console.WriteLine("Failed allocating output stream");
                        ret = ffmpeg.AVERROR_UNKNOWN;
                        return false;
                    }

                    ret = ffmpeg.avcodec_copy_context(out_stream->codec, in_stream->codec);
                    if (ret < 0)
                    {
                        Console.WriteLine("Failed to copy context from input to output stream codec context");
                        return false;
                    }
                    out_stream->codec->codec_tag = 0;
                    if ((ofmt_ctx->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
                        out_stream->codec->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
                }



                ffmpeg.av_dump_format(ofmt_ctx, 0, out_filename, 1);

                if ((ofmt->flags & ffmpeg.AVFMT_NOFILE) == 0)
                {
                    ret = ffmpeg.avio_open(&ofmt_ctx->pb, out_filename, ffmpeg.AVIO_FLAG_WRITE);
                    if (ret < 0)
                    {
                        Console.WriteLine($"Could not open output file '{out_filename}'");
                        return false;
                    }
                }

                ret = ffmpeg.avformat_write_header(ofmt_ctx, null);
                if (ret < 0)
                {
                    Console.WriteLine("Error occurred when opening output file");
                    return false;
                }

                ret = ffmpeg.av_seek_frame(ifmt_ctx, -1, (long)(from_seconds * ffmpeg.AV_TIME_BASE), ffmpeg.AVSEEK_FLAG_ANY);
                if (ret < 0)
                {
                    Console.WriteLine("Error seek");
                    return false;
                }

                IntPtr ip_dts_start_from = Marshal.AllocHGlobal(sizeof(long) * (int)ifmt_ctx->nb_streams);
                long* dts_start_from = (long*)ip_dts_start_from.ToPointer();
                Unsafe.InitBlockUnaligned(dts_start_from, 0, sizeof(long) * ifmt_ctx->nb_streams);

                IntPtr ip_pts_start_from = Marshal.AllocHGlobal(sizeof(long) * (int)ifmt_ctx->nb_streams);
                long* pts_start_from = (long*)ip_pts_start_from.ToPointer();
                Unsafe.InitBlockUnaligned(pts_start_from, 0, sizeof(long) * ifmt_ctx->nb_streams);



                while (true)
                {
                    AVStream* in_stream;
                    AVStream* out_stream;

                    ret = ffmpeg.av_read_frame(ifmt_ctx, &pkt);
                    if (ret < 0)
                        break;

                    in_stream = ifmt_ctx->streams[pkt.stream_index];
                    out_stream = ofmt_ctx->streams[pkt.stream_index];

                    //log_packet(ifmt_ctx, &pkt, "in");

                    if (ffmpeg.av_q2d(in_stream->time_base) * pkt.pts > end_seconds)
                    {
                        ffmpeg.av_packet_unref(&pkt);
                        break;
                    }

                    if (dts_start_from[pkt.stream_index] == 0)
                    {
                        dts_start_from[pkt.stream_index] = pkt.dts;
                        //Console.WriteLine("dts_start_from: " + av_ts2str(dts_start_from[pkt.stream_index]));
                    }
                    if (pts_start_from[pkt.stream_index] == 0)
                    {
                        pts_start_from[pkt.stream_index] = pkt.pts;
                        //Console.WriteLine("pts_start_from: " + av_ts2str(pts_start_from[pkt.stream_index]));
                    }

                    /* copy packet */
                    pkt.pts = ffmpeg.av_rescale_q_rnd(pkt.pts - pts_start_from[pkt.stream_index], in_stream->time_base, out_stream->time_base, AVRounding.AV_ROUND_NEAR_INF | AVRounding.AV_ROUND_PASS_MINMAX);
                    pkt.dts = ffmpeg.av_rescale_q_rnd(pkt.dts - dts_start_from[pkt.stream_index], in_stream->time_base, out_stream->time_base, AVRounding.AV_ROUND_NEAR_INF | AVRounding.AV_ROUND_PASS_MINMAX);
                    if (pkt.pts < 0)
                    {
                        pkt.pts = 0;
                    }
                    if (pkt.dts < 0)
                    {
                        pkt.dts = 0;
                    }
                    pkt.duration = (int)ffmpeg.av_rescale_q(pkt.duration, in_stream->time_base, out_stream->time_base);
                    pkt.pos = -1;
                    //log_packet(ofmt_ctx, &pkt, "out");
                    //Console.WriteLine("\n");

                    ret = ffmpeg.av_interleaved_write_frame(ofmt_ctx, &pkt);
                    if (ret < 0)
                    {
                        Console.WriteLine("Error muxing packet");
                        break;
                    }

                    ffmpeg.av_packet_unref(&pkt);
                }
                Marshal.FreeHGlobal(ip_dts_start_from);
                Marshal.FreeHGlobal(ip_pts_start_from);

                ffmpeg.av_write_trailer(ofmt_ctx);
            }
            finally
            {
                ffmpeg.avformat_close_input(&ifmt_ctx);

                /* close output */
                if (ofmt_ctx != null && (ofmt->flags & ffmpeg.AVFMT_NOFILE) == 0)
                    ffmpeg.avio_closep(&ofmt_ctx->pb);
                ffmpeg.avformat_free_context(ofmt_ctx);

                if (ret < 0 && ret != ffmpeg.AVERROR_EOF)
                {
                    Console.WriteLine($"Error occurred: {ret}");
                }
            }
            return true;
        }

        static string av_ts2str(long time)
        {
            return $"{time / (double)ffmpeg.AV_TIME_BASE}:0.00";
        }
    }
}
