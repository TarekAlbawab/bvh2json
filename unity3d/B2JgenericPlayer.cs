﻿using UnityEngine;

using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using MiniJSON;

namespace B2J {

	public class B2JgenericPlayer : MonoBehaviour {
		
		public B2Jserver B2Jserver;
		protected Transform[] all_transforms;
		protected Dictionary< string, B2Jmap > maps;
		protected Dictionary< string, B2JmaskConfig > maskConfigs;
		protected Dictionary< string, B2Jblender > blenderByModel; // preprocessing of the mocap blend happens here
		protected List< B2Jblender > blenderList;
		protected Dictionary< string, B2Jplayhead > playheadDict;
		protected List< B2Jplayhead > playheadList;
		
		protected Dictionary < Transform, Matrix4x4 > world2local;
		protected Dictionary < string, Transform > armature;
		// if weights[ key ] is -1 -> no config for the transform, 0 will be used 
		protected Dictionary< Transform, float > totalWeights; 
		protected Dictionary< Transform, Quaternion > initialQuaternions;
		protected Dictionary< Transform, Vector3 > initialTranslations;
		protected Dictionary< Transform, Vector3 > initialScales;
		
//		protected Dictionary< Transform, Quaternion > updatedQuaternions;
//		protected Dictionary< Transform, Vector3 > updatedTranslations;
//		protected Dictionary< Transform, Vector3 > updatedScales;
		
		protected Dictionary< Transform, Quaternion > quaternions;
		protected Dictionary< Transform, Vector3 > translations;
		protected Dictionary< Transform, Vector3 > scales;
		
//		protected Dictionary< Transform, float > weights;
		
		protected bool interpolate;
		protected bool rotationNormalise;
		protected bool translationNormalise;
		protected bool scaleNormalise;
		protected B2Jloop defaultLoop;

		protected bool verbose;
		private bool forceSync;

		public B2JgenericPlayer() {
			
			B2Jserver = null;
			all_transforms = null;
			maps = new Dictionary< string, B2Jmap >();
			maskConfigs = new Dictionary< string, B2JmaskConfig >();
			blenderByModel = new Dictionary< string, B2Jblender >();
			blenderList = new List< B2Jblender >();
			playheadDict = new Dictionary< string, B2Jplayhead >();
			playheadList = new List< B2Jplayhead > ();
			
			// making a copy of the current object rotations and orientations
			world2local = new Dictionary < Transform, Matrix4x4 >();
			armature = new Dictionary < string, Transform > ();
			totalWeights = new Dictionary< Transform, float > ();
			
			initialQuaternions = new Dictionary< Transform, Quaternion > ();
			initialTranslations = new Dictionary< Transform, Vector3 > ();
			initialScales = new Dictionary< Transform, Vector3 > ();
			
			quaternions = new Dictionary< Transform, Quaternion > ();
			translations = new Dictionary< Transform, Vector3 > ();
			scales = new Dictionary< Transform, Vector3 > ();
			
//			updatedQuaternions = new Dictionary< Transform, Quaternion >();
//			updatedTranslations = new Dictionary< Transform, Vector3 >();
//			updatedScales = new Dictionary< Transform, Vector3 >();
//			weights = new Dictionary< Transform, float > ();
			
			interpolate = true;
			rotationNormalise = true;
			translationNormalise = true;
			scaleNormalise = true;

			defaultLoop = B2Jloop.B2JLOOP_NORMAL;

			verbose = true;
			forceSync = true;

		}

		public void Quiet() {
			verbose = false;
		}
		
		public void Verbose() {
			verbose = true;
		}

		protected void InitPlayer() {
			
			all_transforms = GetComponentsInChildren<Transform>();
			foreach( Transform t in all_transforms ) {
				
				armature.Add( t.name, t );
				world2local.Add( t, t.worldToLocalMatrix );
				
				initialQuaternions.Add( t, new Quaternion( t.localRotation.x, t.localRotation.y, t.localRotation.z, t.localRotation.w ) );
				initialTranslations.Add( t, new Vector3( t.localPosition.x, t.localPosition.y, t.localPosition.z ) );
				initialScales.Add( t, new Vector3( t.localScale.x, t.localScale.y, t.localScale.z ) );
				
				quaternions.Add( t, Quaternion.identity );
				translations.Add( t, Vector3.zero );
				scales.Add( t, Vector3.one );

				totalWeights.Add( t, -1 );
				
			}

			reset ();
			
		}

		private void reset() {
		
			foreach( Transform t in all_transforms ) {
				quaternions[ t ] = B2Jutils.copy( initialQuaternions[ t ] );
				translations[ t ] = B2Jutils.copy( initialTranslations[ t ] );
				scales[ t ] = B2Jutils.copy( initialScales[ t ] );
				totalWeights[ t ] = -1;
			}

		}

		public void LoadMapping( TextAsset asset ) {
			B2Jmap map = new B2Jmap();
			if ( map.load( asset, this ) ) {
				if ( maps.ContainsKey( map.model ) && verbose ) {
					Debug.LogError( "A map with the same model as already been loaded! It will be overwritten by the current one: " + map.name );
				}
				maps.Add( map.model, map );
				// creating related blender
				B2Jblender mapblend = new B2Jblender( 
                     map,
				     world2local,
                     initialQuaternions,
                     initialTranslations,
                     initialScales
                     );
				blenderList.Add( mapblend  );
				blenderByModel.Add( map.model, mapblend );
				forceSync = true;
			}
		}

		public void LoadMask( string path ) {
			B2JmaskConfig mc = B2JmaskConfigLoader.load ( path, all_transforms );
			if ( mc == null ) {
				return;
			}
			maskConfigs.Add( mc.name, mc );
		}

		public void ApplyMaskOnBlender( string blenderModel, string maskName ) {
			B2JmaskConfig mc = maskConfigs [maskName];
			B2Jblender bb = blenderByModel[ blenderModel ];
			if ( mc != null && bb != null ) {
				bb.getMask().goTo( mc );
			}
		}

		public void ResetMaskOnBlender( string blenderModel ) {
			B2Jblender bb = blenderByModel[ blenderModel ];
			if (  bb != null ) {
				bb.getMask().reset();
			}
		}
		
		public B2Jplayhead GetPlayhead( string name ) {
			foreach( B2Jplayhead ph in playheadList )
				if ( ph.Name == name )
					return ph;
			return null;
		}
		
		protected void Process() {

			synchronise();
			
			// all playheads are now ok
			foreach( B2Jplayhead ph in playheadList ) {
				ph.update( interpolate );
			}

		}
		
		private void synchronise() {

			if ( B2Jserver != null ) {

				bool smthchanged = B2Jserver.SyncPlayheads( playheadList, playheadDict, defaultLoop );

				if ( smthchanged || forceSync ) {

					// first, checking if there some playheads have been destroyed
					foreach( B2Jblender mb in blenderList ) {

						foreach( B2Jplayhead mb_ph in mb.playheads ) {

							bool found = false;

							foreach ( B2Jplayhead ph in playheadList ) {
								if ( ph == mb_ph ) {
									found = true;
									break;
								}
							}

							if ( !found ) {

								mb.playheads.Remove( mb_ph );

							}

						}

					}

					// then checking new ones
					foreach ( B2Jplayhead ph in playheadList ) {
					
						if ( blenderByModel.ContainsKey( ph.Model ) ) {

							B2Jblender mb = blenderByModel[ ph.Model ];

							bool found = false;

							foreach( B2Jplayhead mb_ph in mb.playheads ) {
							
								if ( ph == mb_ph ) {
									found = true;
									break;
								}
							
							}

							if ( !found ) {

								mb.playheads.Add( ph );
								if ( verbose )
									Debug.Log ( "new map blend added " + ph.Model + " >> " + ph.Name );

							}

						} else {

							Debug.LogError( "the player have no map for this model! '" + ph.Model + "'" );

						}
					
					}
					
					if ( verbose )
						Debug.Log ( "One or several playheads have been added or removed from the list!" );
					// map blend have to be checked!

					forceSync = false;

				}

			}

		}
		
		protected void Render() {
			


			// all values and weights to initial ones
			reset();

			// updating all the blenders +
			// --the blenders use MASKS!
			// meaning that each transform may have a different TOTAL WEIGHT:
			// for instance: a blender have a weight of 0 on the arm
			// and another a weight of 1 on the same transform
			// the total weight on this bone is:
			// blender1 weight * arm weight + blender2 weight * arm weight
			// first thing is to check the mask of all blenders and
			// collect the weights transform per transform
			float blenderWeight = 0;
			foreach (B2Jblender bb in blenderList) {

				bb.update( rotationNormalise, translationNormalise, scaleNormalise );
				blenderWeight += bb.getWeight();

				B2Jmask m = bb.getMask();
				if ( m != null ) {

					Dictionary< Transform, float > bweights = m.getWeights();
					foreach( KeyValuePair< Transform, float > pair in bweights ) {
						if ( totalWeights[ pair.Key ] == -1 ) {
							totalWeights[ pair.Key ] = pair.Value * bb.getWeight();
						} else {
							totalWeights[ pair.Key ] += pair.Value * bb.getWeight();
						}
					}

				} else {

					// by default, the weight on each transform is 1
					// meaning the weight of the blender itself
					List< Transform > btransforms = bb.getMap().uniqueTransforms;
					foreach( Transform t in btransforms ) {
						if ( totalWeights[ t ] == -1 ) {
							totalWeights[ t ] = bb.getWeight();
						} else {
							totalWeights[ t ] += bb.getWeight();
						} 
					}

				}

			}


			// now that weights are collected per transform,
			// quaternions, translations and scales can be rendered
			foreach ( B2Jblender bb in blenderList ) {

				if ( bb.getWeight() == 0 )
					continue;

				Dictionary< Transform, Quaternion > qts = bb.getQuaternions();
				Dictionary< Transform, Vector3 > tls = bb.getTranslations();
				Dictionary< Transform, Vector3 > scs = bb.getScales();
				B2Jmask mask = bb.getMask();
				B2Jmap map = bb.getMap();

				// each blender have infos for all the transform it manages
				// THUS:
				List< Transform > btransforms = map.uniqueTransforms;
				foreach( Transform t in btransforms ) {

					float tw = totalWeights[ t ];
					float lw = 0;
					if ( mask != null ) {
						lw = mask.getWeights()[ t ] * bb.getWeight();
					} else {
						lw = bb.getWeight();
					}
					if ( tw > 1 ) {
						lw /= tw;
					}

					if ( map.enable_rotations ) {
						if ( rotationNormalise ) {
							quaternions[ t ] = Quaternion.Slerp( quaternions[ t ], qts[ t ], tw );
						} else {
							quaternions[ t ] *= qts[ t ];
						}
					}

					if ( map.enable_translations ) {
						if ( translationNormalise ) {
							translations[ t ] = B2Jutils.VectorSlerp( translations[ t ], tls[ t ], tw );
						} else {
							translations[ t ] += tls[ t ];
						}
					}

					if ( map.enable_scales ) {
						if ( scaleNormalise ) {
							scales[ t ] = B2Jutils.VectorSlerp( scales[ t ], scs[ t ], tw );
						} else {
							scales[ t ] += scs[ t ];
						}
					}

				}
			
//				float bw = 1;
//				B2Jmask m = bb.getMask();
//				if ( rotationNormalise || translationNormalise || scaleNormalise ) {
//					if ( m != null ) {
//
//					} else {
//						if ( blenderWeight > 1 ) {
//							bw = bb.getWeight() / blenderWeight;
//						} else {
//							bw = bb.getWeight();
//						}
//					}
//				}
//
//				Dictionary< Transform, Quaternion > qts = bb.getQuaternions();
//				foreach( KeyValuePair< Transform, Quaternion > pair in qts ) {
//					if ( rotationNormalise ) {
//						quaternions[ pair.Key ] = Quaternion.Slerp( quaternions[ pair.Key ], pair.Value, bw );
//					} else {
//						quaternions[ pair.Key ] *= pair.Value;
//					}
//				}
//
//				Dictionary< Transform, Vector3 > tls = bb.getTranslations();
//				foreach( KeyValuePair< Transform, Vector3 > pair in tls ) {
//					if ( translationNormalise ) {
//						translations[ pair.Key ] = B2Jutils.VectorSlerp( translations[ pair.Key ], pair.Value, bw );
//					} else {
//						translations[ pair.Key ] += pair.Value;
//					}
//				}
//				
//				Dictionary< Transform, Vector3 > scs = bb.getScales();
//				foreach( KeyValuePair< Transform, Vector3 > pair in scs ) {
//					if ( scaleNormalise ) {
//						scales[ pair.Key ] = B2Jutils.VectorSlerp( scales[ pair.Key ], pair.Value, bw );
//					} else {
//						scales[ pair.Key ] += pair.Value;
//					}
//				}

			}

		}
		
	}

}
