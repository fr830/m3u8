﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace m3u8.ext
{
    /// <summary>
    /// 
    /// </summary>
    internal static class Extensions
    {
        public static bool IsNullOrEmpty( this string s )
        {
            return (string.IsNullOrEmpty( s ));
        }
        public static bool IsNullOrWhiteSpace( this string s )
        {
            return (string.IsNullOrWhiteSpace( s ));
        }

        public static string AsPartExceptionMessage( this string responseJson )
        {
            return (responseJson.IsNullOrWhiteSpace() ? string.Empty : ($", '{responseJson}'"));
        }
        public static string CreateExceptionMessage( this HttpResponseMessage response, string responseJson )
        {
            return ($"{(int) response.StatusCode}, {response.ReasonPhrase}{responseJson.AsPartExceptionMessage()}");
        }

        public static bool AnyEx< T >( this IEnumerable< T > seq )
        {
            return (seq != null && seq.Any());
        }

        public static string ReadAsStringAsyncEx( this HttpContent content, CancellationToken ct )
        {
            var t = content.ReadAsStringAsync();
            t.Wait( ct );
            return (t.Result);
        }
        public static byte[] ReadAsByteArrayAsyncEx( this HttpContent content, CancellationToken ct )
        {
            var t = content.ReadAsByteArrayAsync();
            t.Wait( ct );
            return (t.Result);
        }
        /*public static Task< T > WithCancellation< T >( this Task< T > task, CancellationToken ct )
        {
            if ( task.IsCompleted )
            {
                return (task);
            }

            var continuetask = task.ContinueWith( completedTask => completedTask.GetAwaiter().GetResult(),
                                                  ct,
                                                  TaskContinuationOptions.ExecuteSynchronously,
                                                  TaskScheduler.Default );
            return (continuetask);
        }*/
    }

    /// <summary>
    /// 
    /// </summary>
    internal struct DefaultConnectionLimitSaver : IDisposable
    {
        private int _DefaultConnectionLimit;
        private DefaultConnectionLimitSaver( int connectionLimit )
        {
            _DefaultConnectionLimit = ServicePointManager.DefaultConnectionLimit;
            ServicePointManager.DefaultConnectionLimit = connectionLimit;
        }
        public static DefaultConnectionLimitSaver Create( int connectionLimit )
        {
            return (new DefaultConnectionLimitSaver( connectionLimit ));
        }
        public void Dispose()
        {
            ServicePointManager.DefaultConnectionLimit = _DefaultConnectionLimit;
        }
    }
}

namespace m3u8
{
    using System.IO;
    using m3u8.ext;

    /// <summary>
    /// 
    /// </summary>
    public struct m3u8_part_ts
    {
        public m3u8_part_ts( string relativeUrlName, int orderNumber ) : this()
        {
            RelativeUrlName = relativeUrlName;
            OrderNumber     = orderNumber;
        }

        public string RelativeUrlName { get; private set; }
        public int OrderNumber { get; private set; }

        public byte[] Bytes { get; private set; }
        public void SetBytes( byte[] bytes )
        {
            Bytes = bytes;
        }

        public Exception Error { get; private set; }
        public void SetError( Exception error )
        {
            Error = error;
        }
#if DEBUG
        public override string ToString()
        {
            return ($"{OrderNumber}, '{RelativeUrlName}'");
        }
#endif
    }
    /// <summary>
    /// 
    /// </summary>
    public struct m3u8_part_ts_comparer: IComparer< m3u8_part_ts >
    {
        public int Compare( m3u8_part_ts x, m3u8_part_ts y )
        {
            return (x.OrderNumber - y.OrderNumber);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public struct m3u8_file_t
    {
        public IReadOnlyList< m3u8_part_ts > Parts { get; private set; }

        public Uri BaseAddress { get; private set; }

        public string RawText { get; private set; }

        public static m3u8_file_t Parse( string content, Uri baseAddress )
        {
            var lines = from row in content.Split( new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries )
//---.Take( 50 )
                        let line = row.Trim()
                        where (!line.IsNullOrEmpty() && !line.StartsWith( "#" ))
                        select line
                        ;
            var parts = lines.Select( (line, i) => new m3u8_part_ts( line, i ) );
            var o = new m3u8_file_t()
            {
                Parts       = parts.ToList().AsReadOnly(),
                BaseAddress = baseAddress,
                RawText     = content,
            };
            return (o);
        }
    }


    /// <summary>
    /// 
    /// </summary>
    public static class m3u8_Extensions
    {
        //private static string Unwrap4DialogMessage( this SmartCATArgumentException ex )
        //{
        //    return ((ex != null) ? $"SmartCATArgumentException: {ex.Message} => '{ex.ParamName}'" : null);
        //}

        public static string Unwrap4DialogMessage( this Exception ex, out bool isCanceledException )
        {
            isCanceledException = false;

            var cex = ex as OperationCanceledException;
            if ( cex != null )
            {
                isCanceledException = true;
                return (cex.Message);
            }

            var sex = ex as m3u8_ArgumentException;
            if ( sex != null )
            {
                return ($"SmartCATArgumentException: '{sex.Message} => [{sex.ParamName}]'");
                //return (sex.Unwrap4DialogMessage());
            }

            var aex = ex as AggregateException;
            if ( aex != null )
            {
                if ( aex.InnerExceptions.All( ( _ex ) => _ex is OperationCanceledException ) )
                {
                    isCanceledException = true;
                    return (aex.InnerExceptions.FirstOrDefault()?.Message);
                }

                if ( aex.InnerExceptions.Count == 1 )
                {
                    if ( aex.InnerException is m3u8_Exception )
                    {
                        return ("SmartCATException: '" + ((m3u8_Exception) aex.InnerException).Message + '\'');
                    }
                    else if ( aex.InnerException is HttpRequestException )
                    {
                        var message = "HttpRequestException: '";
                        for ( Exception x = ((HttpRequestException) aex.InnerException); x != null; x = x.InnerException )
                        {
                            message += x.Message + Environment.NewLine;
                        }
                        return (message + '\'');
                    }
                    else
                    {
                        return (ex.GetType().Name + ": '" + ex.ToString() + '\'');
                    }
                }
            }

            return (ex.ToString());
        }

        public static string Unwrap4DialogMessage( this Exception ex, bool ignoreCanceledException = true )
        {
            bool isCanceledException;
            var message = ex.Unwrap4DialogMessage( out isCanceledException );
            return (isCanceledException ? null : message);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public sealed class m3u8_ArgumentException : ArgumentNullException
    {
        public m3u8_ArgumentException() : base() { }
        public m3u8_ArgumentException( string paramName ) : base( paramName ) { }
        public m3u8_ArgumentException( string message, Exception innerException ) : base( message, innerException ) { }
        public m3u8_ArgumentException( string paramName, string message ) : base( paramName, message ) { }
    }
    /// <summary>
    /// 
    /// </summary>
    public sealed class m3u8_Exception : HttpRequestException
    {
        public m3u8_Exception() : base() { }
        public m3u8_Exception( string message ) : base( message ) { }
        public m3u8_Exception( string message, Exception inner ) : base( message, inner ) { }
    }

    /// <summary>
    /// 
    /// </summary>
    public sealed class m3u8_client : IDisposable
    {
        /// <summary>
        /// 
        /// </summary>
        public struct init_params
        {
            //---public Uri BaseAddress { get; set; }
            public int?      AttemptRequestCount { get; set; }
            public TimeSpan? Timeout             { get; set; }
            public bool?     ConnectionClose     { get; set; }
        }


        private HttpClient _HttpClient;

        #region [.ctor().]
        public m3u8_client( init_params ip )
        {
            //if ( p.BaseAddress == null ) throw (new SmartCATArgumentException( nameof(p.BaseAddress) ));

            InitParams = ip;

            _HttpClient = new HttpClient();
            //_HttpClient.BaseAddress = p.BaseAddress;
            _HttpClient.DefaultRequestHeaders.ConnectionClose = ip.ConnectionClose; //true => //.KeepAlive = false, .Add("Connection", "close");
            if ( ip.Timeout.HasValue )
            {
                _HttpClient.Timeout = ip.Timeout.Value;
            };
        }

        public void Dispose()
        {
            _HttpClient.Dispose();
        }
        #endregion

        public init_params InitParams
        {
            get;
            private set;
        }

        public async Task< m3u8_file_t > DownloadFile( Uri url
            , CancellationToken? cancellationToken = null )
        {
            if ( url == null ) throw (new m3u8_ArgumentException( nameof(url) ));

            var ct = cancellationToken.GetValueOrDefault( CancellationToken.None );
            var attemptRequestCountByPart = InitParams.AttemptRequestCount.GetValueOrDefault( 1 );

            for ( var attemptRequestCount = attemptRequestCountByPart; 0 < attemptRequestCount; attemptRequestCount-- )
            {
                try
                {
                    using ( HttpResponseMessage response = await _HttpClient.GetAsync( url, ct ) )
                    using ( HttpContent content = response.Content )
                    {
                        if ( !response.IsSuccessStatusCode )
                        {
                            var json = content.ReadAsStringAsyncEx( ct );
                            throw (new m3u8_Exception( response.CreateExceptionMessage( json ) ));
                        }

                        var text = content.ReadAsStringAsyncEx( ct );
                        var m3u8File = m3u8_file_t.Parse( text, url );
                        return (m3u8File);
                    }
                }
                catch ( Exception ex )
                {
                    if ( (attemptRequestCount == 1) || ct.IsCancellationRequested )
                    {
                        throw (ex);
                    }
                }
            }

            throw (new m3u8_Exception( $"No content found while {attemptRequestCountByPart} attempt requests" ));
        }

        public async Task< m3u8_part_ts > DownloadPart( m3u8_part_ts part
            , Uri baseAddress
            , CancellationToken? cancellationToken = null )
        {
            if ( baseAddress == null ) throw (new m3u8_ArgumentException( nameof(baseAddress) ));
            if ( part.RelativeUrlName.IsNullOrWhiteSpace() ) throw (new m3u8_ArgumentException( nameof(part.RelativeUrlName) ));

            var url = new Uri( baseAddress, part.RelativeUrlName );
            var ct = cancellationToken.GetValueOrDefault( CancellationToken.None );
            var attemptRequestCountByPart = InitParams.AttemptRequestCount.GetValueOrDefault( 1 );

            for ( var attemptRequestCount = attemptRequestCountByPart; 0 < attemptRequestCount; attemptRequestCount-- )
            {
                try
                {
                    using ( HttpResponseMessage response = await _HttpClient.GetAsync( url, ct ) )
                    using ( HttpContent content = response.Content )
                    {
                        if ( !response.IsSuccessStatusCode )
                        {
                            var json = content.ReadAsStringAsyncEx( ct );
                            throw (new m3u8_Exception( response.CreateExceptionMessage( json ) ));
                        }

                        var bytes = content.ReadAsByteArrayAsyncEx( ct ); //---var bytes = await content.ReadAsByteArrayAsync();
                        part.SetBytes( bytes );
                        return (part);
                    }
                }
                catch ( Exception ex )
                {
                    if ( (attemptRequestCount == 1) || ct.IsCancellationRequested )
                    {
                        part.SetError( ex );
                        return (part);
                    }
                }
            }

            throw (new m3u8_Exception( $"No content found while {attemptRequestCountByPart} attempt requests" ));
        }


        public static m3u8_client CreateDefault( int attemptRequestCountByPart = 10 )
        {
            var ip = new init_params()
            {
                AttemptRequestCount = attemptRequestCountByPart,
            };
            var mc = new m3u8_client( ip );
            return (mc);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public static class m3u8_processor
    {
        /// <summary>
        /// 
        /// </summary>
        private struct download_m3u8File_parts_parallel_params_t
        {
            public download_m3u8File_parts_parallel_params_t( m3u8_client _mc, m3u8_file_t _m3u8File, DownloadFileAndSaveInputParams ip ) : this()
            {
                mc                     = _mc;
                m3u8File               = _m3u8File;
                cts                    = ip.Cts;
                stepAction             = ip.StepAction;
                progressStepAction     = ip.ProgressStepAction;
                maxDegreeOfParallelism = ip.MaxDegreeOfParallelism;
            }
            public download_m3u8File_parts_parallel_params_t( DownloadPartsAndSaveInputParams ip ) : this()
            {
                mc                     = ip.mc;
                m3u8File               = ip.m3u8File;
                cts                    = ip.Cts;
                stepAction             = ip.StepAction;
                progressStepAction     = ip.ProgressStepAction;
                maxDegreeOfParallelism = ip.MaxDegreeOfParallelism;
            }

            public m3u8_client mc       { get; set; }
            public m3u8_file_t m3u8File { get; set; }

            public CancellationTokenSource    cts { get; set; }
            public StepActionDelegate         stepAction { get; set; }
            public ProgressStepActionDelegate progressStepAction { get; set; }
            public int                        maxDegreeOfParallelism { get; set; }
        }

        private static IEnumerable< m3u8_part_ts > download_m3u8File_parts_parallel( download_m3u8File_parts_parallel_params_t ip )
        {
            var ct = (ip.cts?.Token).GetValueOrDefault( CancellationToken.None );
            var baseAddress = ip.m3u8File.BaseAddress;
            var totalPatrs  = ip.m3u8File.Parts.Count;
            var successReceivedPartCount = 0;
            var failedReceivedPartCount  = 0;

            ip.progressStepAction?.Invoke( new ProgressStepActionParams( totalPatrs ) );

            var expectedPartNumber = ip.m3u8File.Parts.FirstOrDefault().OrderNumber;
            var maxPartNumber      = ip.m3u8File.Parts.LastOrDefault ().OrderNumber;
            var sourceQueue        = new Queue< m3u8_part_ts >( ip.m3u8File.Parts );
            var downloadPartsSet   = new SortedSet< m3u8_part_ts >( default(m3u8_part_ts_comparer) );

            using ( DefaultConnectionLimitSaver.Create( ip.maxDegreeOfParallelism ) )
            using ( var canExtractPartEvent = new AutoResetEvent( false ) )
            using ( var semaphore = new SemaphoreSlim( ip.maxDegreeOfParallelism ) )
            {
                //-1-//
                var task_download = Task.Run( () =>
                {
                    for ( var n = 1; sourceQueue.Count != 0; n++ )
                    {
                        semaphore.Wait( ct );
                        var part = sourceQueue.Dequeue();

                        var p = StepActionParams.CreateSuccess( totalPatrs, n, part );
                        ip.stepAction?.Invoke( p ); //---var ar = ip.stepAction?.BeginInvoke( p, null, null ); ip.stepAction?.EndInvoke( ar );

                        ip.mc.DownloadPart( part, baseAddress, ct )
                            .ContinueWith( (continuationTask) =>
                            {
                                var ep = new ProgressStepActionParams( totalPatrs );

                                if ( continuationTask.IsFaulted )
                                {
                                    Interlocked.Increment( ref expectedPartNumber );

                                    ip.stepAction?.Invoke( p.SetError( continuationTask.Exception ) );
                                    ep.SuccessReceivedPartCount = successReceivedPartCount;
                                    ep.FailedReceivedPartCount  = Interlocked.Increment( ref failedReceivedPartCount );

                                    ip.progressStepAction?.Invoke( ep );
                                }
                                else if ( !continuationTask.IsCanceled )
                                {
                                    var downloadPart = continuationTask.Result;
                                    if ( downloadPart.Error != null )
                                    {
                                        ip.stepAction?.Invoke( p.SetError( downloadPart.Error ) );
                                        ep.SuccessReceivedPartCount = successReceivedPartCount;
                                        ep.FailedReceivedPartCount  = Interlocked.Increment( ref failedReceivedPartCount );
                                    }
                                    else
                                    {
                                        ep.SuccessReceivedPartCount = Interlocked.Increment( ref successReceivedPartCount );
                                        ep.FailedReceivedPartCount  = failedReceivedPartCount;
                                    }
                                    ip.progressStepAction?.Invoke( ep );

                                    lock ( downloadPartsSet )
                                    {
                                        downloadPartsSet.Add( downloadPart );
                                        canExtractPartEvent.Set();
                                    }
                                }
                            }, ct );
                    }
                }, ct );

                //-2-//
                for ( var localReadyParts = new Queue< m3u8_part_ts >( Math.Min( 0x1000, ip.maxDegreeOfParallelism ) ); 
                        expectedPartNumber <= maxPartNumber /*&& !ct.IsCancellationRequested*/; )
                {
//#if DEBUG
//                    CONSOLE.WriteLine( $"wait part #{expectedPartNumber}..." ); 
//#endif
                    var idx = WaitHandle.WaitAny( new[] { canExtractPartEvent, ct.WaitHandle } );
                    if ( idx == 1 ) //ct.IsCancellationRequested
                        break;
                    if ( idx != 0 ) //[canExtractPartEvent := 0]
                        continue;

                    lock ( downloadPartsSet )
                    {
                        for ( ; downloadPartsSet.Count != 0; )
                        {
                            var min_part = downloadPartsSet.Min;
                            if ( expectedPartNumber == min_part.OrderNumber )
                            {
                                //CONSOLE.WriteLine( $"receive part #{expectedPartNumber}." );

                                downloadPartsSet.Remove( min_part );

                                Interlocked.Increment( ref expectedPartNumber );

                                semaphore.Release();

                                localReadyParts.Enqueue( min_part );
                            }
                            else
                            {
                                break;
                            }
                        }
                    }

                    for ( ; localReadyParts.Count != 0; )
                    {
                        var part = localReadyParts.Dequeue();
                        yield return (part);
                    }

                    #region comm. prev.
                    /*
                    lock ( downloadPartsSet )
                    {
                        for ( ; downloadPartsSet.Count != 0; )
                        {
                            var min_part = downloadPartsSet.Min;
                            if ( expectedPartNumber == min_part.OrderNumber )
                            {
                                //CONSOLE.WriteLine( $"receive part #{expectedPartNumber}." );

                                downloadPartsSet.Remove( min_part );

                                Interlocked.Increment( ref expectedPartNumber );

                                semaphore.Release();

                                yield return (min_part);
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    */
                    #endregion
                }

                //-3-//
                task_download.Wait();
            }

            ct.ThrowIfCancellationRequested();
        }
        //-----------------------------------------------------------------------------//

        /// <summary>
        /// 
        /// </summary>
        public struct StepActionParams
        {
            public int          TotalPartCount  { get; private set; }
            public int          PartOrderNumber { get; private set; }
            public m3u8_part_ts Part            { get; private set; }
            public Exception    Error           { get; private set; }
            public bool         Success         => (Error == null);

            internal StepActionParams SetError( Exception error )
            {
                Error = error;
                return (this);
            }

            internal static StepActionParams CreateSuccess( int totalPartCount, int partOrderNumber, m3u8_part_ts part )
            {
                var o = new StepActionParams() { TotalPartCount = totalPartCount, PartOrderNumber = partOrderNumber, Part = part };
                return (o);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public delegate void StepActionDelegate( StepActionParams p );
        /// <summary>
        /// 
        /// </summary>
        public struct ProgressStepActionParams
        {
            internal ProgressStepActionParams( int totalPartCount ) : this()
            {
                TotalPartCount = totalPartCount;
            }

            public int TotalPartCount           { get; private set; }
            public int SuccessReceivedPartCount { get; internal set; }
            public int FailedReceivedPartCount  { get; internal set; }
        }
        /// <summary>
        /// 
        /// </summary>
        public delegate void ProgressStepActionDelegate( ProgressStepActionParams p );

        /// <summary>
        /// 
        /// </summary>
        public struct DownloadFileAndSaveInputParams
        {
            public const int DEFAULT_MAXDEGREEOFPARALLELISM    = 64;
            public const int DEFAULT_ATTEMPTREQUESTCOUNTBYPART = 10;

            public string m3u8FileUrl    { get; set; }
            public string OutputFileName { get; set; }

            public CancellationTokenSource    Cts { get; set; }
            public StepActionDelegate         StepAction { get; set; }
            public ProgressStepActionDelegate ProgressStepAction { get; set; }

            private int? _MaxDegreeOfParallelism;
            public int MaxDegreeOfParallelism
            {
                get { return (_MaxDegreeOfParallelism.GetValueOrDefault( DEFAULT_MAXDEGREEOFPARALLELISM )); }
                set { _MaxDegreeOfParallelism = Math.Max( 1, value ); }
            }

            private m3u8_client.init_params? _NetParams;
            public m3u8_client.init_params NetParams
            {
                get
                {
                    if ( !_NetParams.HasValue )
                    {
                        _NetParams = new m3u8_client.init_params()
                        {
                            AttemptRequestCount = DEFAULT_ATTEMPTREQUESTCOUNTBYPART,
                        };
                    }
                    return (_NetParams.Value);
                }
                set
                {
                    value.AttemptRequestCount = Math.Max( 1, value.AttemptRequestCount.GetValueOrDefault( DEFAULT_ATTEMPTREQUESTCOUNTBYPART ) );
                    _NetParams = value;
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public struct DownloadFileAndSaveResult
        {
            internal DownloadFileAndSaveResult( DownloadFileAndSaveInputParams ip ) : this()
            {
                m3u8FileUrl    = ip.m3u8FileUrl;
                OutputFileName = ip.OutputFileName;
            }

            public string m3u8FileUrl    { get; internal set; }
            public string OutputFileName { get; internal set; }

            public int PartsSuccessCount { get; internal set; }
            public int PartsErrorCount   { get; internal set; }
            public int TotalBytes        { get; internal set; }

            public int TotalParts => (PartsSuccessCount + PartsErrorCount);
        }

        public static async Task< DownloadFileAndSaveResult > DownloadFileAndSave_Async( DownloadFileAndSaveInputParams ip )
        {
            if ( ip.m3u8FileUrl.IsNullOrWhiteSpace() ) throw (new m3u8_ArgumentException( nameof(ip.m3u8FileUrl) ));
            if ( ip.OutputFileName.IsNullOrWhiteSpace() ) throw (new m3u8_ArgumentException( nameof(ip.OutputFileName) ));
            //----------------------------------------------------------------------------------------//

            var m3u8FileUrl = new Uri( ip.m3u8FileUrl );

            using ( var mc = new m3u8_client( ip.NetParams ) )
            {
                var ct = (ip.Cts?.Token).GetValueOrDefault( CancellationToken.None );
                var res = new DownloadFileAndSaveResult( ip );

                await Task.Run( async () =>
                {
                    //-1-//
                    var m3u8File = await mc.DownloadFile( m3u8FileUrl, ct );

                    //-2-//
                    var tp = new download_m3u8File_parts_parallel_params_t( mc, m3u8File, ip );                    
                    var downloadParts = download_m3u8File_parts_parallel( tp );

                    //-3-//                    
                    using ( var fs = File.OpenWrite( ip.OutputFileName ) )
                    {
                        fs.SetLength( 0 );

                        foreach ( var downloadPart in downloadParts )
                        {
                            if ( downloadPart.Error != null ) //|| downloadPart.Bytes == null )
                            {
                                res.PartsErrorCount++;
                                continue;
                            }
                            var bytes = downloadPart.Bytes;
                            fs.Write( bytes, 0, bytes.Length );

                            res.PartsSuccessCount++;
                            res.TotalBytes += bytes.Length;
                        }
                    }

                }, ct );

                return (res);
            }
        }
        //-----------------------------------------------------------------------------//

        /// <summary>
        /// 
        /// </summary>
        public struct DownloadPartsAndSaveInputParams
        {
            public m3u8_client mc       { get; set; }
            public m3u8_file_t m3u8File { get; set; }
            public string OutputFileName { get; set; }

            public CancellationTokenSource    Cts { get; set; }
            public StepActionDelegate         StepAction { get; set; }
            public ProgressStepActionDelegate ProgressStepAction { get; set; }
            public int                        MaxDegreeOfParallelism { get; set; }
        }
        /// <summary>
        /// 
        /// </summary>
        public struct DownloadPartsAndSaveResult
        {
            internal DownloadPartsAndSaveResult( string outputFileName ) : this()
            {
                OutputFileName = outputFileName;
            }

            public string OutputFileName { get; internal set; }

            public int PartsSuccessCount { get; internal set; }
            public int PartsErrorCount   { get; internal set; }
            public int TotalBytes        { get; internal set; }

            public int TotalParts => (PartsSuccessCount + PartsErrorCount);
        }

        public static DownloadPartsAndSaveResult DownloadPartsAndSave( DownloadPartsAndSaveInputParams ip )
        {            
            if ( ip.mc == null ) throw (new m3u8_ArgumentException( nameof(ip.mc) ));
            if ( !ip.m3u8File.Parts.AnyEx() ) throw (new m3u8_ArgumentException( nameof(ip.m3u8File) ));
            if ( ip.OutputFileName.IsNullOrWhiteSpace() ) throw (new m3u8_ArgumentException( nameof(ip.OutputFileName) ));
            //----------------------------------------------------------------------------------------//

            //-1-//
            var res = new DownloadPartsAndSaveResult( ip.OutputFileName );

            //-2-//
            var tp = new download_m3u8File_parts_parallel_params_t( ip );
            var downloadParts = download_m3u8File_parts_parallel( tp );            

            //-3-//
            using ( var fs = File.OpenWrite( ip.OutputFileName ) )
            {
                fs.SetLength( 0 );

                foreach ( var downloadPart in downloadParts )
                {
                    if ( downloadPart.Error != null ) //|| downloadPart.Bytes == null )
                    {
                        res.PartsErrorCount++;
                        continue;
                    }
                    var bytes = downloadPart.Bytes;
                    fs.Write( bytes, 0, bytes.Length );

                    res.PartsSuccessCount++;
                    res.TotalBytes += bytes.Length;
                }
            }

            return (res);
        }
        //-----------------------------------------------------------------------------//
    }
}