%% Get Phases
% This script calculates all the gazing times.
% Hierarchy: calcPhasesV2 -> getPhasesV2

% For yielding
% 1)start sound till start trigger (deceleration
% 2)start deceleration till end deceleration
% 3)standstill (up to 2.6 seconds).
% No phases needed for non-yielding
% 1) start sound till past zebra crossing

% Author: Johnson Mok
% Last Updated: 11-03-2021

function out = getPhaseV2(data, ED)
out.idx = getPhaseIdx(data,ED);
out.time = out.idx*data.dt; 
out.pos = getPhasePosTime(data,out.idx);
end

%% Helper functions
function out = getIdxYield(data,EDnr)
pos = data.pa.pos.z;
pos_rel = pos - 17.19;
dx = abs(gradient(pos_rel));

% indices yield
idx_start = 1;
% if not mapping gaze to yield
if(EDnr ~= 4 || EDnr ~= 6)
    idx_decel_start = find(pos<40.44,1,'first');
elseif (EDnr == 4 || EDnr == 6)
    idx_decel_start = find(data.pa.distance<25,1,'first');
end
idx_standstill_start = find(dx<0.01, 1,'first');
idx_standstill_end = idx_standstill_start + round(2.6/0.0167);

% Set up phases
ph1 = [idx_start; idx_decel_start];
ph2 = [idx_decel_start; idx_standstill_start];
ph3 = [idx_standstill_start; idx_standstill_end];

% Set up idx array
out = [ph1, ph2, ph3];

%% Test 
if (false)
    figure
    subplot(2,1,1)
    plot(data.pa.pos.z)
    hold on
    xline(idx_decel_start,'--',{'start decel'});
    xline(idx_standstill_start,'--',{'start standstill'});
    xline(idx_standstill_end,'--',{'end standstill'});
    title('pos');

    subplot(2,1,2)
    plot(data.pa.world.rb_v.z)
    hold on
    xline(idx_decel_start,'--',{'start decel'});
    xline(idx_standstill_start,'--',{'start standstill'});
    xline(idx_standstill_end,'--',{'end standstill'});
    title('velocity');
end
end
function out = getIdxNonYield(data)
pos = data.pa.pos.z;
pos_rel = pos - 17.19;

% indices yield
idx_start = 1;
idx_past_zebra = find(pos_rel< - 4.69, 1, 'first');

% Set up phases
ph1 = [idx_start; idx_past_zebra];

% Set up idx array
out = ph1;

%% Test 
if (false)
    figure
    subplot(2,1,1)
    plot(data.pa.pos.z)
    hold on
    xline(idx_past_zebra,'--',{'past zebra'});
    title('pos');

    subplot(2,1,2)
    plot(data.pa.world.rb_v.z)
    hold on
    xline(idx_past_zebra,'--',{'past zebra'});
    title('velocity');
end

end
function idx = getPhaseIdx(data,ED)
ED_split = split(ED,'_');
EDnr = str2double(ED_split{3}); % ED 4 and 6 use velocity to determine restart, the other yielding ED use 2.6 seconds since standstill
NY = [1, 3, 5, 7, 9, 11];

if(isempty(find(NY==EDnr)))
    idx = getIdxYield(data,EDnr);
elseif (~isempty(find(NY==EDnr)))
    idx = getIdxNonYield(data);
end
end

function out = getPhasePosTime(data,idx)
pos = data.pa.pos.z;
pos_rel = pos - 17.19;
i1 = idx(1,1):idx(2,1);
out.ph1 = [i1'*data.dt, pos_rel(i1)];
if(length(idx)>2) 
    i2 = idx(1,2):idx(2,2);
    out.ph2 = [i2'*data.dt, pos_rel(i2)];
    i3 = idx(1,3):idx(2,3);
    out.ph3 = [i3'*data.dt, pos_rel(i3)];
end
end





