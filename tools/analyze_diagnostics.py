"""Read a diagnostic session directory or exported ZIP without extracting it.
Findings are leads for review, not proof that an unused ability should have fired.
"""
import argparse
from collections import Counter, defaultdict
import gzip
import json
from pathlib import Path
import zipfile

JOBS = dict(Drg=22, Machinist=31, Bard=23, Viper=41, Reaper=39, Summoner=27,
            RedMage=35, Pictomancer=42, BlackMage=25, Samurai=34, Monk=20,
            Ninja=30, Dancer=38, BlueMage=36, Beastmaster=43, Paladin=19,
            Warrior=21, Gunbreaker=37, DarkKnight=32, Sage=40, WhiteMage=24, Scholar=28, Astrologian=33)


def analyze(files):
    counts = defaultdict(Counter)
    features = defaultdict(Counter)
    rejected = Counter()
    summaries, builds, markers, warnings, states = {}, {}, [], [], {}
    worlds = {}
    enabled, jobs = set(), set()
    sends, effects = [], []
    sequence = {}
    end = 0
    for name, lines in files:
        if name.endswith('summary.json'):
            summary = json.loads(''.join(lines))
            summaries[summary['Component']] = summary
            continue
        if not name.endswith('.jsonl'):
            continue
        for number, raw in enumerate(lines, 1):
            try:
                event = json.loads(raw)
            except (ValueError, UnicodeError):
                warnings.append(f'Malformed/incomplete record: {name}:{number}')
                continue
            component, kind = event['Component'], event['Kind']
            data = event.get('Data') or {}
            at = event['MonotonicMs']
            end = max(end, at)
            seq = event['Sequence']
            stream_id = (component, event.get('Session'))
            previous = sequence.get(stream_id, 0)
            if seq != previous + 1:
                warnings.append(f'{component}: event sequence gap {previous} -> {seq}')
            sequence[stream_id] = seq
            counts[component][kind] += 1
            if kind == 'Session.Start': builds[component] = data
            if kind == 'User.Marker': markers.append(dict(Time=at, **data))
            if kind == 'World':
                worlds[component] = data
                if component == 'WrathCombo': jobs.add(data.get('Job'))
            if kind == 'State':
                states[component] = data
                if component == 'WrathCombo':
                    enabled.update(k for k, value in data.get('Experimental', {}).items() if value is True)
            if kind == 'Feature.Evaluated': features[data['Feature']]['active' if data['Active'] else 'inactive'] += 1
            if kind in ('Rejected', 'Runner.Exit', 'Resolver.Rejected'):
                rejected[(component, data.get('Reason'), data.get('File'), data.get('Line'))] += 1
            if kind == 'Native.Send': sends.append((at, data))
            if kind in ('Server.Effect', 'Server.NativeEffect'):
                # Only our own action effects can confirm our sends.
                player = worlds.get(component, {}).get('PlayerEntityId')
                if player is not None and data.get('Actor') == player:
                    effects.append((at, data))
    for component in ('Accord', 'WrathCombo', 'RotationSolver'):
        if component not in builds: warnings.append(f'{component}: no session-start/build record; component coverage missing')
        if component not in summaries: warnings.append(f'{component}: no final summary; session may still be recording or ended unexpectedly')
        summary = summaries.get(component, {})
        if summary.get('ExpiredParts'):
            warnings.append(f"{component}: rolling retention expired {summary['ExpiredParts']} old parts; only the beginning and recent history remain")
        if summary.get('Dropped') or summary.get('Error'):
            warnings.append(f'{component}: incomplete capture: lost={summary.get("Dropped", 0)} error={summary.get("Error", "")}')
    never_reached = [name for name in sorted(enabled) if name in JOBS and JOBS[name] in jobs
                     and not features['ActiveHealer' if name in ('WhiteMage','Scholar','Astrologian') else 'Active' + name]['active']]
    unmatched = []
    indexed_effects = defaultdict(list)
    for at, data in effects: indexed_effects[(data.get('Action'), data.get('SourceSequence'))].append(at)
    for at, data in sends:
        if end - at < 30000: continue  # don't call an unfinished tail a missing confirmation
        if not any(0 <= effect_at - at <= 30000 for effect_at in indexed_effects[(data.get('Action'), data.get('SourceSequence'))]):
            unmatched.append(dict(Time=at, **data))
    return dict(Schema=1, Interpretation='Review leads only. No observed opportunity is not a rotation failure. Unmatched sends may be interrupted, rejected, or outside capture coverage.',
                Builds=builds, Warnings=warnings, Markers=markers, EnabledJobFeaturesNeverReached=never_reached,
                FeatureEvaluations={k: dict(v) for k,v in features.items()}, Events={k: dict(v) for k,v in counts.items()},
                MostFrequentRejections=[dict(Component=k[0], Reason=k[1], File=k[2], Line=k[3], Count=v) for k,v in rejected.most_common(100)],
                SendsWithoutObservedConfirmation=unmatched, FinalStates=states, Summaries=summaries)


def read(path):
    if path.is_dir():
        for file in sorted(path.rglob('*')):
            if file.name.endswith('.jsonl.gz'):
                with gzip.open(file, 'rt', encoding='utf-8-sig') as stream: yield str(file.relative_to(path))[:-3], stream
            elif file.is_file() and (file.suffix == '.jsonl' or file.name == 'summary.json'):
                with file.open(encoding='utf-8-sig') as stream: yield str(file.relative_to(path)), stream
    else:
        import io
        with zipfile.ZipFile(path) as archive:
            for name in sorted(archive.namelist()):
                if name.endswith('.jsonl.gz'):
                    with archive.open(name) as raw, gzip.open(raw, 'rt', encoding='utf-8-sig') as stream:
                        yield name[:-3], stream
                elif name.endswith('.jsonl') or name.endswith('/summary.json'):
                    with archive.open(name) as stream:
                        yield name, io.TextIOWrapper(stream, encoding='utf-8-sig')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('session', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    result = json.dumps(analyze(read(args.session)), indent=2)
    if args.output: args.output.write_text(result + '\n', encoding='utf-8')
    else: print(result)
