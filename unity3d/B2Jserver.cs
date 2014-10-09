﻿using UnityEngine;

using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

using B2J;

namespace B2J {

	public class B2Jserver: MonoBehaviour {
		
		private Dictionary< string, B2Jrecord > _loadedpath;
		private List<string> _loadingpath;
		private List<B2Jrecord> _records;

		private bool newRecord;

		private bool verbose;

		public B2Jserver() {
			_loadedpath = new Dictionary< string, B2Jrecord > ();
			_loadingpath = new List<string> ();
			_records = new List<B2Jrecord> ();
			newRecord = false;
			verbose = false;
		}

		public void setQuiet() {
			verbose = false;
		}

		public void setVerbose() {
			verbose = true;
		}

		public void Start() {}

		public void Update() {}
		
		public void OnApplicationQuit() {}

		public void OnDestroy() {}

		private void load( string path ) {
			if ( _loadedpath.ContainsKey ( path ) ) {
				if ( verbose )
					Debug.Log ( "'" + path + "' already loaded" );
				return;
			}
			addNewRecord( B2Jparser.Instance.load ( path ), path );
		}
		
		public void addNewRecord( B2Jrecord rec, string path ) {
			if ( rec != null ) {
				_loadedpath.Add( path, rec );
				_records.Add( rec );
				newRecord = true;
				if ( verbose )
					Debug.Log ( "new record added: " + rec.name + ", " + _records.Count + " record(s) loaded" );
			}
		}

		public bool syncPlayheads( List< string > syncRequests, List< B2Jplayhead > phs, Dictionary< string, B2Jplayhead > dict, B2Jloop loop ) {

			bool modified = false;

			if ( !newRecord && syncRequests.Count == 0 && phs.Count == _records.Count )
				return modified;

			// is there playheads not registered anymore?
			foreach ( B2Jplayhead ph in phs ) {
				if ( ! _records.Contains( ph.getRecord() ) ) {
					phs.Remove( ph );
					dict.Remove( ph.getName() );
					modified = true;
				}
			}

			// is there sync requests?
			if ( syncRequests.Count > 0 ) {
				List< string > tmpreqs = new List< string > (syncRequests);
				foreach( string path in tmpreqs ) {

					load( path );

					if ( _loadedpath.ContainsKey ( path ) ) {

						B2Jplayhead ph = createNewPlayhead( _loadedpath[ path ], phs, loop );
						dict.Add( ph.getName(), ph );
						modified = true;

					} else {

						Debug.LogError( "Impossible to load the record '" + path +"'" );

					}

					syncRequests.Remove( path );

				}
			}

			// new records may have been loaded, creating a new playhead if required
//			foreach (B2Jrecord rec in _records) {
//				bool found = false;
//				foreach ( B2Jplayhead ph in phs ) {
//					if ( ph.getRecord() == rec ) {
//						found = true;
//						break;
//					}
//				}
//				if ( !found ) {
//					B2Jplayhead ph = createNewPlayhead( rec, phs, loop );
//					dict.Add( ph.getName(), ph );
//					modified = true;
//				}
//			}
			
			newRecord = false;

			return modified;
		
		}

		private B2Jplayhead createNewPlayhead( B2Jrecord rec, List< B2Jplayhead > phs, B2Jloop loop ) {
		
			B2Jplayhead ph = new B2Jplayhead ( rec, loop );
			phs.Add ( ph );
			return ph;
		
		}

		
		public void printRecord( B2Jrecord br ) {
			
			if (br == null) {
				Debug.Log ("BVH2JSON: record is empty" );
			} else {
				Debug.Log ("BVH2JSON************");
				Debug.Log ( br.name );
				for ( int i = 0; i < br.groups.Count; i++ ) {
					Debug.Log ( "group[" + i + "] = " + br.groups[ i ].name + " (" + br.groups[ i ].use_millis + "," + br.groups[ i ].use_keys + ")" );
				}
				for ( int i = 0; i < br.bones.Count; i++ ) {
					Debug.Log ( "bone[" + i + "] = " + br.bones[ i ].name + " (" + br.bones[ i ].positions_enabled + "," +  br.bones[ i ].rotations_enabled  + "," +  br.bones[ i ].scales_enabled + ")" );
					if ( br.bones[ i ].parent != null ) {
						Debug.Log ( "\t\tparent:" + br.bones[ i ].parent.name );
					}
					foreach( B2Jbone child in br.bones[ i ].children ) {
						Debug.Log ( "\t\tchild:" + child.name );
					}
					
				}
				for ( int i = 0; i < br.keys.Count; i++ ) {
					Debug.Log ( "key[" + i + "] = " + br.keys[ i ].kID + " / " + br.keys[ i ].timestamp );
				}
				Debug.Log ("BVH2JSON************");
			}
		}
		
	}

}
