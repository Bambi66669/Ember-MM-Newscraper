﻿' ################################################################################
' #                             EMBER MEDIA MANAGER                              #
' ################################################################################
' ################################################################################
' # This file is part of Ember Media Manager.                                    #
' #                                                                              #
' # Ember Media Manager is free software: you can redistribute it and/or modify  #
' # it under the terms of the GNU General Public License as published by         #
' # the Free Software Foundation, either version 3 of the License, or            #
' # (at your option) any later version.                                          #
' #                                                                              #
' # Ember Media Manager is distributed in the hope that it will be useful,       #
' # but WITHOUT ANY WARRANTY; without even the implied warranty of               #
' # MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the                #
' # GNU General Public License for more details.                                 #
' #                                                                              #
' # You should have received a copy of the GNU General Public License            #
' # along with Ember Media Manager.  If not, see <http://www.gnu.org/licenses/>. #
' ################################################################################

Imports EmberAPI
Imports MovieCollection
Imports NLog
Imports System.Net.Http
Imports System.Text.RegularExpressions

Public Class Scraper

#Region "Fields"

    Shared _Logger As Logger = LogManager.GetCurrentClassLogger()

    Private _addonSettings As OMDb_Data.SpecialSettings
    Private _Options As OpenMovieDatabase.OpenMovieDatabaseOptions

    Private _Client As OpenMovieDatabase.OpenMovieDatabaseService = Nothing
    Private _HttpClient As HttpClient = Nothing

#End Region 'Fields

#Region "Properties"

    Public ReadOnly Property IsClientCreated As Boolean
        Get
            Return _Client IsNot Nothing
        End Get
    End Property

#End Region 'Properties

#Region "Methods"

    Public Sub CreateAPI(ByVal addonSettings As OMDb_Data.SpecialSettings)
        If Not String.IsNullOrEmpty(addonSettings.APIKey) Then
            Try
                _addonSettings = addonSettings

                _HttpClient = New HttpClient
                _Options = New OpenMovieDatabase.OpenMovieDatabaseOptions(addonSettings.APIKey)
                _Client = New OpenMovieDatabase.OpenMovieDatabaseService(_HttpClient, _Options)
                _Logger.Trace("[OMDb_Data] [CreateAPI] Client created")
            Catch ex As Exception
                _Client = Nothing
                _Logger.Error(String.Format("[OMDb_Data] [CreateAPI] [Error] {0}", ex.Message))
            End Try
        Else
            _Logger.Error("[OMDb_Data] [CreateAPI] [Error] No API key available")
            _Client = Nothing
        End If
    End Sub
    ''' <summary>
    '''  Scrape MovieDetails from OMDb
    ''' </summary>
    ''' <param name="ImdbId">IMDb ID of movie to be scraped</param>
    ''' <returns>True: success, false: no success</returns>
    Public Function GetInfo_Movie(ByVal ImdbId As String, ByVal FilteredOptions As Structures.ScrapeOptions) As MediaContainers.Movie
        If String.IsNullOrEmpty(ImdbId) Then Return Nothing

        Dim APIResult As Task(Of OpenMovieDatabase.Models.Movie) = Nothing
        Dim intTMDBID As Integer = -1

        If ImdbId.ToLower.StartsWith("tt") Then
            'search movie by IMDB ID
            APIResult = Task.Run(Function() _Client.SearchMovieByImdbIdAsync(ImdbId))
            'ElseIf Integer.TryParse(ImdbId, intTMDBID) Then
            '    'search movie by TMDB ID
            '    APIResult = Task.Run(Function() _Client.GetMovieAsync(intTMDBID, TMDbLib.Objects.Movies.MovieMethods.Credits Or TMDbLib.Objects.Movies.MovieMethods.Releases Or TMDbLib.Objects.Movies.MovieMethods.Videos))
        Else
            _Logger.Error(String.Format("Can't scrape or movie not found: [0]", ImdbId))
            Return Nothing
        End If

        If APIResult Is Nothing OrElse APIResult.Result Is Nothing OrElse String.IsNullOrEmpty(APIResult.Result.ImdbId) OrElse APIResult.Exception IsNot Nothing Then
            _Logger.Error(String.Format("Can't scrape or movie not found: [0]", ImdbId))
            Return Nothing
        End If

        Dim Result As OpenMovieDatabase.Models.Movie = APIResult.Result
        Dim nMovie As New MediaContainers.Movie With {.Scrapersource = "OMDb"}

        'Rating
        If FilteredOptions.bMainRating Then
            'IMDb rating
            If _addonSettings.IMDb AndAlso Result.ImdbRating IsNot Nothing AndAlso Result.ImdbVotes IsNot Nothing Then
                Dim dblRating As Double
                Dim iVotes As Integer
                If Double.TryParse(Result.ImdbRating, dblRating) AndAlso Integer.TryParse(NumUtils.CleanVotes(Result.ImdbVotes), iVotes) Then
                    nMovie.Ratings.Add(New MediaContainers.RatingDetails With {
                                       .Max = 10,
                                       .Name = "imdb",
                                       .Value = dblRating,
                                       .Votes = iVotes
                                       })
                End If
            End If
            'Metascore
            If _addonSettings.Metascore AndAlso Result.Metascore IsNot Nothing Then
                Dim dblRating As Double
                If Double.TryParse(Result.Metascore, dblRating) Then
                    nMovie.Ratings.Add(New MediaContainers.RatingDetails With {
                                       .Max = 100,
                                       .Name = "metacritic",
                                       .Value = dblRating
                                       })
                End If
            End If
            'Rotten Tomatoes
            For Each tRating In Result.Ratings
                Select Case tRating.Source.ToLower
                    Case "rotten tomatoes"
                        If _addonSettings.Tomatometer Then
                            Dim dblRating As Double
                            Dim strRating = Regex.Match(tRating.Value, "\d*").Value
                            If Double.TryParse(strRating, dblRating) Then
                                nMovie.Ratings.Add(New MediaContainers.RatingDetails With {
                                                   .Max = 100,
                                                   .Name = "tomatometerallcritics",
                                                   .Value = dblRating
                                                   })
                            End If
                        End If
                End Select
            Next
        End If
        Return nMovie
    End Function

#End Region 'Methods

End Class